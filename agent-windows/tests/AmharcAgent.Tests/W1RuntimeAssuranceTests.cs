using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Clock;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class W1RuntimeAssuranceTests
{
    private sealed class Time : TimeProvider
    {
        public DateTimeOffset Utc = DateTimeOffset.Parse("2026-10-04T10:00:00Z");
        public long Timestamp;
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => Timestamp;
        public override long TimestampFrequency => 1000;
        public void Advance(int seconds) { Utc += TimeSpan.FromSeconds(seconds); Timestamp += seconds * 1000; }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "w1-runtime-" + Guid.NewGuid());
        public readonly byte[] RootSeed = RandomNumberGenerator.GetBytes(32);
        public readonly Time Time = new();
        public W1SqliteClockJournal Journal { get; }
        public JsonObject Identity { get; }
        public JsonObject Context { get; }
        public W1DevelopmentGrantVerifier Verifier { get; }
        public string PublicKey { get; }
        public string Subject => Identity["occurrenceId"]!.GetValue<string>();
        public Fixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            Journal = new(Path.Combine(DirectoryPath, "synthetic.sqlite"));
            var frame = JsonNode.Parse(File.ReadAllText(Path.Combine(W1ControlledVectorTests.DependenciesDirectory(),
                "W1_CONFORMANCE_VECTORS.json")))!["cases"]![0]!["input"]!["snapshot"]!;
            Identity = (JsonObject)frame["subject"]!.DeepClone();
            Context = (JsonObject)frame["context"]!.DeepClone();
            var key = new Ed25519PrivateKeyParameters(RootSeed, 0).GeneratePublicKey();
            var der = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key).GetEncoded();
            var pem = "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(der) + "\n-----END PUBLIC KEY-----\n";
            PublicKey = pem;
            Verifier = new(new(pem, new HashSet<string> { Subject }, "official-match-accumulated"));
        }
        public W1SubjectBoundRuntime Runtime(IW1ClockJournal? journal = null) =>
            new(Identity, Context, journal ?? Journal, "synthetic-capture", "candidate-test-build", "p1",
                W1ControlledVectorTests.LoadDependencies(), Time);
        public W1VerifiedDevelopmentGrant Grant(W1SubjectBoundRuntime runtime, params string[] capabilities)
        {
            var bundle = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(
                W1ControlledVectorTests.DependenciesDirectory(), "fixtures", "trustBundle.json")))!;
            bundle["grant"]!["incarnationId"] = runtime.IncarnationId;
            bundle["principalPublicKey"] = PublicKey;
            bundle["grant"]!["capabilities"] = new JsonArray(capabilities.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
            var digest = W1CanonicalJson.Digest(bundle.ToJsonString(), "governanceProof");
            bundle["governanceProof"] = new JsonObject {
                ["payloadSha256"] = digest, ["signatureBase64"] = W1CanonicalJson.Sign(digest, RootSeed)
            };
            return Verifier.Verify(bundle, Subject, runtime.IncarnationId);
        }
        public void Dispose() { CryptographicOperations.ZeroMemory(RootSeed); Directory.Delete(DirectoryPath, true); }
    }

    [Fact]
    public void K07_Restart_NewIncarnationNeverInheritsRunningEligibility()
    {
        using var f = new Fixture();
        var first = f.Runtime(); var restart = f.Runtime();
        Assert.NotEqual(first.IncarnationId, restart.IncarnationId);
        Assert.Throws<InvalidOperationException>(() => restart.Capture(f.Subject));
    }
    [Fact]
    public void K08_SnapshotCapabilityDoesNotAuthoriseAdvanceOrCorrection()
    {
        using var f = new Fixture(); var run = f.Runtime();
        run.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(run, "snapshot"), "activate");
        Assert.Throws<InvalidOperationException>(() => run.Tick(f.Subject));
        Assert.Throws<InvalidOperationException>(() => run.Transition(f.Subject, "correct", "correct", 60, basis: "test"));
        Assert.Equal(0, run.Capture(f.Subject)["clock"]!["accumulated"]!.GetValue<int>());
    }
    [Fact]
    public void K17_OldWriterCannotRewriteGovernedContext()
    {
        using var f = new Fixture();
        var activity = new W1JournalActivity("test-activity", f.Subject, "operation", new string('a', 64), "{}");
        Assert.Throws<InvalidOperationException>(() => f.Journal.Commit(0,
            new(f.Subject, 1, "0.9", "{}", activity.Id), activity));
        Assert.Null(f.Journal.Read(f.Subject));
        Assert.Null(f.Journal.FindOperation(f.Subject, "operation"));
    }
    [Fact]
    public void K21_CheckpointHistoryCommitFailure_RollsBackBothAndMemory()
    {
        using var f = new Fixture();
        var journal = new W1SqliteClockJournal(Path.Combine(f.DirectoryPath, "fault.sqlite"),
            () => throw new IOException("synthetic-before-commit"));
        var run = f.Runtime(journal);
        Assert.Throws<IOException>(() => run.AdoptVerifiedDevelopmentGrant(f.Subject,
            f.Grant(run, "snapshot", "checkpoint"), "activate"));
        Assert.Null(journal.Read(f.Subject));
        Assert.Null(journal.FindOperation(f.Subject, "activate"));
        Assert.Throws<InvalidOperationException>(() => run.Capture(f.Subject));
    }
    [Fact]
    public void K22_RestoreVerifiesHistoricalDependencies_NewActivityPausedReconstruction()
    {
        using var f = new Fixture(); var first = f.Runtime();
        var caps = new[] { "snapshot", "advance", "pause-resume", "correct", "checkpoint", "recover" };
        first.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(first, caps), "activate");
        first.Transition(f.Subject, "correct", "initial-correction", 120, basis: "synthetic qualified correction");
        first.Transition(f.Subject, "checkpoint", "checkpoint");
        var restart = f.Runtime();
        restart.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(restart, caps), "new-incarnation-grant");
        Assert.Throws<InvalidOperationException>(() => restart.Capture(f.Subject));
        restart.Restore(f.Subject, "recovery", f.Verifier, W1ControlledVectorTests.LoadDependencies());
        var restored = restart.Capture(f.Subject);
        Assert.Equal(120, restored["clock"]!["accumulated"]!.GetValue<int>());
        Assert.Equal("paused", restored["clock"]!["state"]!.GetValue<string>());
        Assert.Equal("utc-reconstructed", restored["clock"]!["method"]!.GetValue<string>());
        Assert.Equal(restart.IncarnationId, restored["authority"]!["incarnationId"]!.GetValue<string>());
        Assert.NotNull(f.Journal.FindOperation(f.Subject, "recovery"));
    }
    [Fact]
    public void K06_LegacyUnknownBindingIsNotPromotedIntoQualifiedRecovery()
    {
        using var f = new Fixture(); var run = f.Runtime();
        Assert.Throws<InvalidOperationException>(() =>
            run.Restore(f.Subject, "legacy-restore", f.Verifier, W1ControlledVectorTests.LoadDependencies()));
        Assert.Null(f.Journal.Read(f.Subject));
    }
    [Fact]
    public void WrongSubjectRuntimeSnapshot_IsRefusedBeforeOrderOrHistoryAllocation()
    {
        using var f = new Fixture(); var run = f.Runtime();
        run.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(run, "snapshot"), "activate");
        Assert.Throws<InvalidOperationException>(() => run.Capture(Guid.NewGuid().ToString("D")));
        Assert.Equal("1", run.Capture(f.Subject)["authority"]!["sequence"]!.GetValue<string>());
        Assert.Equal(1, f.Journal.Read(f.Subject)!.Revision);
    }
    [Fact]
    public void PeriodApplicabilityCannotBeSuppliedByCallerTruth()
    {
        using var f = new Fixture(); var run = f.Runtime();
        run.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(run, "snapshot", "period-transition"), "activate");
        Assert.Throws<InvalidOperationException>(() => run.Transition(f.Subject, "period", "unknown-period",
            periodKey: "invented-period", applicablePeriodKeys: new HashSet<string> { "invented-period" }));
        Assert.Equal("p1", run.Capture(f.Subject)["clock"]!["periodKey"]!.GetValue<string>());
        Assert.Null(f.Journal.FindOperation(f.Subject, "unknown-period"));
    }
    [Fact]
    public void ActivityRetryIsIdempotentAndRetainsOriginalMaterialRecord()
    {
        using var f = new Fixture(); var run = f.Runtime();
        run.AdoptVerifiedDevelopmentGrant(f.Subject, f.Grant(run, "snapshot", "correct"), "activate");
        run.Transition(f.Subject, "correct", "correction", 120, basis: "synthetic");
        var revision = f.Journal.Read(f.Subject)!.Revision;
        run.Transition(f.Subject, "correct", "correction", 120, basis: "synthetic");
        Assert.Equal(revision, f.Journal.Read(f.Subject)!.Revision);
        Assert.Throws<InvalidOperationException>(() => run.Transition(f.Subject, "correct", "correction", 100, basis: "synthetic"));
    }
    [Fact]
    public void GovernedProducer_ActualComposedEnvelopeAndFailedHeadResolutionAreAtomic()
    {
        using var f = new Fixture(); var run = f.Runtime();
        var grant = f.Grant(run, "snapshot", "correct");
        run.AdoptVerifiedDevelopmentGrant(f.Subject, grant, "activate");
        var resolver = W1ControlledVectorTests.LoadDependencies();
        var head = run.MaterialHead(f.Subject);
        var activity = f.Journal.ReadActivity(f.Journal.Read(f.Subject)!.ActivityId)!;
        resolver.Add(head, System.Text.Encoding.UTF8.GetBytes(activity.Json));
        var closure = grant.CopyEvidence();
        closure["identifier"] = "urn:amharc:test:observation-closure:" + Guid.NewGuid().ToString("D");
        closure["materialHistory"] = new JsonArray(new JsonObject { ["id"] = head.Id, ["sha256"] = head.Sha256 });
        var digest = W1CanonicalJson.Digest(closure.ToJsonString(), "governanceProof");
        closure["governanceProof"] = new JsonObject {
            ["payloadSha256"] = digest, ["signatureBase64"] = W1CanonicalJson.Sign(digest, f.RootSeed)
        };
        var bytes = System.Text.Encoding.UTF8.GetBytes(closure.ToJsonString());
        var closureRef = new W1Reference(closure["identifier"]!.GetValue<string>(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        resolver.Add(closureRef, bytes);
        var contract = W1DependencyResolver.Reference(resolver.Resolve(W1ControlledReferences.Manifest)["exactDependencies"]!["contract"]!);
        var producer = new W1GovernedClockProducer(run, resolver, contract, W1ControlledVectorTests.Schema(),
            closureRef, closure["keyId"]!.GetValue<string>(), f.RootSeed, _ => 2659,
            new(f.PublicKey, new HashSet<string> { f.Subject }, "official-match-accumulated"));
        var envelope = producer.Emit(f.Subject);
        Assert.Equal("1", envelope["authority"]!["sequence"]!.GetValue<string>());
        Assert.Equal("recordingElapsedSeconds", envelope["legacyRecording"]!["coordinate"]!.GetValue<string>());
        Assert.Equal(2659, envelope["legacyRecording"]!["value"]!.GetValue<int>());
        run.Transition(f.Subject, "correct", "new-head", 120, basis: "synthetic");
        Assert.Throws<InvalidOperationException>(() => producer.Emit(f.Subject));
        Assert.Equal("2", run.Capture(f.Subject)["authority"]!["sequence"]!.GetValue<string>());
    }
}