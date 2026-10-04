using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Clock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;
using Match = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public sealed class W1FoundationTests
{
    private static Match MatchRequest() => new() {
        HomeTeam = "Synthetic Home", AwayTeam = "Synthetic Away",
        Competition = "Conformance", Season = "Test", Date = new DateOnly(2026, 10, 4),
        Sport = Sport.GaelicFootball, Status = MatchStatus.Setup
    };

    private static AmharcDbContext Database(string path) => new(
        new DbContextOptionsBuilder<AmharcDbContext>()
            .UseSqlite("Data Source=" + path + ";Default Timeout=30").Options);

    [Fact]
    public async Task K19_ConcurrentCreationRetry_OneDurableOccurrenceAndRepresentation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "w1-creation-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "synthetic.sqlite");
        try
        {
            using (var db = Database(path)) await db.Database.EnsureCreatedAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(async () => {
                await using var db = Database(path);
                return await new W1ProspectiveCreationRepository(db).CreateAsync(
                    "urn:amharc:test:installation-a", "logical-operation-a", MatchRequest(),
                    "synthetic-actor", "test-build",
                    new Dictionary<string, string> {
                        ["urn:amharc:w1:identifier-application:1.0.0"] =
                            "c956baed4ef18c54bcdcf32d1e7019bfe131e655e5609a32ac25c93cf2b650e1"
                    });
            })));
            Assert.Single(results.Select(x => x.OccurrenceId).Distinct());
            Assert.Single(results.Select(x => x.LocalMatchId).Distinct());
            Assert.NotEqual(results[0].OccurrenceId, results[0].LocalMatchId);
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
                results[0].OccurrenceId);
            await using var verify = Database(path);
            Assert.Equal(1, await verify.Matches.CountAsync());
            Assert.Equal(1, await verify.Set<W1OccurrenceCreation>().CountAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task K20_SameOperationDifferentIssuer_DistinctNamespacedCreations()
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-issuers-" + Guid.NewGuid() + ".sqlite");
        try
        {
            await using var db = Database(path);
            await db.Database.EnsureCreatedAsync();
            var deps = new Dictionary<string, string> {
                ["urn:amharc:w1:identifier-application:1.0.0"] =
                    "c956baed4ef18c54bcdcf32d1e7019bfe131e655e5609a32ac25c93cf2b650e1"
            };
            var repo = new W1ProspectiveCreationRepository(db);
            var a = await repo.CreateAsync("test:issuer-a", "op", MatchRequest(), "test", "test", deps);
            var b = await repo.CreateAsync("test:issuer-b", "op", MatchRequest(), "test", "test", deps);
            Assert.NotEqual(a.OccurrenceId, b.OccurrenceId);
            Assert.NotEqual(a.LocalMatchId, b.LocalMatchId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task K18_RepresentationDeletion_PreservesIssuedIdentityAndCorrespondence()
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-retention-" + Guid.NewGuid() + ".sqlite");
        try
        {
            await using var db = Database(path);
            await db.Database.EnsureCreatedAsync();
            var result = await new W1ProspectiveCreationRepository(db).CreateAsync(
                "test:issuer", "op", MatchRequest(), "test", "test",
                new Dictionary<string, string> { ["controlled:test"] = new string('a', 64) });
            db.Matches.Remove(await db.Matches.SingleAsync());
            await db.SaveChangesAsync();
            Assert.Equal(result.OccurrenceId, (await db.Set<W1OccurrenceCreation>().SingleAsync()).OccurrenceId);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task AdditiveMigration_RealSqliteAndRollbackRefusalPreserveIdentityLedger()
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-migration-" + Guid.NewGuid() + ".sqlite");
        try
        {
            await using var db = Database(path);
            await db.Database.MigrateAsync();
            var repo = new W1ProspectiveCreationRepository(db);
            var result = await repo.CreateAsync("test:issuer", "operation", MatchRequest(), "test", "test",
                new Dictionary<string, string> { ["controlled:test"] = new string('a', 64) });
            var migration = new AmharcAgent.Data.Migrations.AddW1ProspectiveIdentity();
            var down = typeof(AmharcAgent.Data.Migrations.AddW1ProspectiveIdentity)
                .GetMethod("Down", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                down.Invoke(migration, new object[] { new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite") }));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Equal(result.OccurrenceId, (await db.Set<W1OccurrenceCreation>().SingleAsync()).OccurrenceId);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { File.Delete(path); }
    }

    private static MatchClockService Clock(Mock<IMatchClockStateStore>? store = null) =>
        new((store ?? new Mock<IMatchClockStateStore>()).Object, NullLogger<MatchClockService>.Instance);

    [Fact]
    public async Task K01_WrongSubjectCommand_RefusesBeforeMutation()
    {
        using var clock = Clock();
        clock.StartFor("representation-a");
        var before = clock.State;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await using var lease = await clock.EnterCommandAsync("representation-b", false);
            clock.Correct(99, "must not execute");
        });
        Assert.Equal(before.MatchClockSeconds, clock.State.MatchClockSeconds);
        Assert.Empty(clock.GetAuditLog());
    }

    [Fact]
    public void K02_WrongSubjectSnapshot_DoesNotAllocateSequence()
    {
        using var clock = Clock();
        clock.StartFor("representation-a");
        var authority = new Mock<IClockAuthorityContext>();
        authority.SetupGet(x => x.Authority).Returns(new ClockAuthorityV1("test", "test"));
        authority.SetupGet(x => x.AuthorityEpoch).Returns(1);
        authority.Setup(x => x.NextSequence()).Returns(1);
        var producer = new CanonicalClockSnapshotService(clock, authority.Object);
        Assert.Throws<InvalidOperationException>(() => producer.CreateSnapshot("representation-b"));
        authority.Verify(x => x.NextSequence(), Times.Never);
        Assert.Equal("representation-a", producer.CreateSnapshot("representation-a").MatchId);
    }

    [Fact]
    public async Task K03_SubjectSwitchPublicationRace_NeverRelabelsState()
    {
        using var clock = Clock();
        clock.StartFor("representation-a");
        clock.Correct(120, "test");
        clock.MarkFullTime();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() => {
            try { return clock.ReadFor("representation-a", s => s.MatchClockSeconds); }
            catch (InvalidOperationException) { return -1; }
        })).ToArray();
        clock.StartFor("representation-b");
        clock.Correct(900, "test-b");
        Assert.All(await Task.WhenAll(tasks), result => Assert.True(result is 120 or -1));
    }

    [Fact]
    public async Task K04_WrongPersistenceSubject_NoCheckpointWrite()
    {
        var store = new Mock<IMatchClockStateStore>();
        using var clock = Clock(store);
        clock.StartFor("representation-a");
        await Assert.ThrowsAsync<InvalidOperationException>(() => clock.SaveRuntimeStateAsync("representation-b"));
        store.Verify(x => x.SaveAsync(It.IsAny<MatchClockRuntimeState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task K05_WrongCheckpointSubject_NoRestore()
    {
        var store = new Mock<IMatchClockStateStore>();
        store.Setup(x => x.LoadAsync("representation-b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MatchClockRuntimeState { MatchId = "representation-a", MatchClockSeconds = 500 });
        using var clock = Clock(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => clock.RecoverRuntimeStateAsync("representation-b"));
        Assert.Equal(0, clock.State.MatchClockSeconds);
    }
    [Fact]
    public void AbandonedContextEndsWithoutFabricatingFullTime_ThenAllowsNewSubject()
    {
        using var clock = Clock();
        clock.StartFor("representation-a");
        clock.EndSubject("representation-a");
        Assert.False(clock.State.IsRunning);
        clock.StartFor("representation-b");
        Assert.Equal(0, clock.ReadFor("representation-b", s => s.MatchClockSeconds));
        Assert.Throws<InvalidOperationException>(() => clock.ReadFor("representation-a", s => s));
    }

    [Fact]
    public void K14_OfficialCorrection_PreservesLegacyRecordingCoordinate()
    {
        using var clock = Clock();
        clock.StartFor("representation-a");
        clock.Pause();
        var before = clock.State.RecordingElapsedSeconds;
        clock.Correct(120, "forward");
        clock.Correct(60, "authorised test downward");
        Assert.Equal(60, clock.State.MatchClockSeconds);
        Assert.InRange(clock.State.RecordingElapsedSeconds, before, before + 1);
    }

    [Theory]
    [InlineData("{\"x\":-0}")]
    [InlineData("{\"x\":1e-400}")]
    [InlineData("{\"x\":2147483648}")]
    [InlineData("{\"x\":1,\"\\u0078\":2}")]
    [InlineData("{\"x\":\"\\ud800\"}")]
    public void CanonicalBytes_RejectLossyOrAmbiguousInput(string json) =>
        Assert.ThrowsAny<Exception>(() => W1CanonicalJson.Canonicalize(json));

    [Fact]
    public void ControlledCrypto_All14Cases_ManagedProductImplementation()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var vectorPath = Path.Combine(directory!.FullName, "governance", "wave-1", "v1.0.0", "W1_CONFORMANCE_VECTORS.json");
        using var vectors = JsonDocument.Parse(File.ReadAllText(vectorPath));
        var cases = vectors.RootElement.GetProperty("cryptographicInteroperability").EnumerateArray().ToArray();
        Assert.Equal(14, cases.Length);
        foreach (var v in cases)
        {
            var raw = v.GetProperty("rawJson").GetString()!;
            if (v.GetProperty("expected").GetString() == "REJECT")
            {
                Assert.ThrowsAny<Exception>(() => W1CanonicalJson.Canonicalize(raw));
                continue;
            }
            var bytes = W1CanonicalJson.Canonicalize(raw);
            Assert.Equal(v.GetProperty("canonicalUtf8Hex").GetString(), Convert.ToHexString(bytes).ToLowerInvariant());
            var digest = W1CanonicalJson.Digest(raw);
            Assert.Equal(v.GetProperty("sha256Hex").GetString(), digest);
            Assert.Equal(v.GetProperty("expected").GetString() == "VALID",
                W1CanonicalJson.Verify(digest, v.GetProperty("publicKeyPem").GetString()!,
                    v.GetProperty("signatureBase64").GetString()!));
        }
    }
}