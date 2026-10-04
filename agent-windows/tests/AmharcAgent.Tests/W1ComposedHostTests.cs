using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AmharcAgent.Api.Controllers;
using AmharcAgent.Api.W1;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class W1ComposedHostTests
{
    private sealed class FixedTime : TimeProvider
    { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-04T10:00:00Z"); }

    [Fact]
    public async Task ActualOperatorRoute_DIHost_Producer_Closure_WrongSubject_Export()
    {
        var directory = Path.Combine(Path.GetTempPath(), "w1-composed-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var seed = RandomNumberGenerator.GetBytes(32);
        var publicDer = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
            new Ed25519PrivateKeyParameters(seed, 0).GeneratePublicKey()).GetEncoded();
        var publicPem = "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(publicDer) + "\n-----END PUBLIC KEY-----\n";
        await File.WriteAllTextAsync(Path.Combine(directory, "public.pem"), publicPem);
        await File.WriteAllTextAsync(Path.Combine(directory, "synthetic-seed"), Convert.ToBase64String(seed));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["W1:DevelopmentOnly"] = "true", ["W1:Issuer"] = "urn:amharc:development:issuer:composed",
            ["W1:Actor"] = "synthetic-http-capture", ["W1:Build"] = "targeted-candidate",
            ["W1:InitialPeriodKey"] = "p1", ["W1:DependencyDirectory"] = W1ControlledVectorTests.DependenciesDirectory(),
            ["W1:ScratchJournalPath"] = Path.Combine(directory, "history.sqlite"),
            ["W1:GovernancePublicKeyFile"] = Path.Combine(directory, "public.pem"),
            ["W1:DevelopmentSigningSeedFile"] = Path.Combine(directory, "synthetic-seed")
        });
        builder.Services.AddSingleton<TimeProvider>(new FixedTime());
        builder.Services.AddDbContext<AmharcDbContext>(o => o.UseSqlite("Data Source=" + Path.Combine(directory, "matches.sqlite")));
        builder.Services.AddScoped<IMatchRepository, MatchRepository>();
        builder.Services.AddSingleton(Mock.Of<IMatchClockService>());
        builder.Services.AddSingleton(Mock.Of<ICanonicalClockSnapshotService>());
        builder.Services.AddSingleton(Mock.Of<IBroadcastPresentationStateService>());
        builder.Services.AddSingleton(Mock.Of<ILiveReadinessService>());
        builder.Services.AddSingleton(Mock.Of<IAmharcCommandDispatcher>());
        builder.Services.AddSingleton(Mock.Of<IOverlayService>());
        builder.Services.AddW1DevelopmentComposition(builder.Configuration, builder.Environment);
        builder.Services.AddControllers().AddApplicationPart(typeof(MatchesController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        await using var host = builder.Build();
        host.MapControllers();
        using (var scope = host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AmharcDbContext>().Database.EnsureCreatedAsync();
        await host.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
        try
        {
            var request = new { sport = "gaelic-football", competition = "Synthetic", season = "2026",
                date = "2026-10-04", homeTeam = "Synthetic A", awayTeam = "Synthetic B" };
            async Task<JsonObject> Create()
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/matches") { Content = JsonContent.Create(request) };
                message.Headers.Add("Idempotency-Key", "one-operator-intent");
                var response = await http.SendAsync(message);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                return (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            }
            var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Create()));
            Assert.Single(created.Select(c => c["matchId"]!.GetValue<string>()).Distinct());
            var local = created[0]["matchId"]!.GetValue<string>();
            using var conflictCreation = new HttpRequestMessage(HttpMethod.Post, "/api/matches") {
                Content = JsonContent.Create(new { sport = "gaelic-football", competition = "Changed",
                    season = "2026", date = "2026-10-04", homeTeam = "Synthetic A", awayTeam = "Synthetic B" })
            };
            conflictCreation.Headers.Add("Idempotency-Key", "one-operator-intent");
            Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(conflictCreation)).StatusCode);
            string subject;
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AmharcDbContext>();
                var ledger = await db.Set<AmharcAgent.Core.Domain.W1OccurrenceCreation>().SingleAsync();
                subject = ledger.OccurrenceId; Assert.NotEqual(local, subject);
                Assert.Equal(local, ledger.LocalMatchId); Assert.Single(await db.Matches.ToListAsync());
            }
            var missingKey = await http.PostAsJsonAsync("/api/matches", request);
            Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
            var resolution = new JsonObject {
                ["identifier"] = "urn:amharc:development:resolution:" + subject,
                ["occurrenceId"] = subject, ["identityStanding"] = "provisional",
                ["representation"] = new JsonObject { ["issuer"] = "urn:amharc:development:issuer:composed", ["localId"] = local }
            };
            var prepareResponse = await http.PostAsJsonAsync("/api/w1-development/prepare/" + local, resolution);
            prepareResponse.EnsureSuccessStatusCode();
            var prepare = JsonNode.Parse(await prepareResponse.Content.ReadAsStringAsync())!;
            var bundle = (JsonObject)JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(
                W1ControlledVectorTests.DependenciesDirectory(), "fixtures/trustBundle.json")))!;
            bundle["identifier"] = "urn:amharc:development:grant:" + subject;
            bundle["closure"]!["subject"] = subject; bundle["grant"]!["subject"] = subject;
            bundle["grant"]!["incarnationId"] = prepare["incarnationId"]!.DeepClone();
            bundle["grant"]!["capabilities"] = new JsonArray("snapshot", "pause-resume", "correct", "checkpoint", "recover", "period-transition");
            bundle["resolutionRef"] = prepare["resolutionRef"]!.DeepClone();
            bundle["principalPublicKey"] = publicPem;
            void Sign(JsonObject b)
            {
                var digest = W1CanonicalJson.Digest(b.ToJsonString(), "governanceProof");
                b["governanceProof"] = new JsonObject { ["payloadSha256"] = digest,
                    ["signatureBase64"] = W1CanonicalJson.Sign(digest, seed) };
            }
            Sign(bundle);
            // Authority Gap before external grant, not a legacy fallback.
            var gapResponse = await http.GetAsync("/api/w1-development/clock/" + subject);
            Assert.Equal(HttpStatusCode.Conflict, gapResponse.StatusCode);
            var gapRaw = await gapResponse.Content.ReadAsStringAsync();
            (await http.PostAsJsonAsync("/api/w1-development/activate/" + subject + "/activation", bundle)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/w1-development/command/" + subject,
                new { operation = "resume", operationKey = "no-advance-proof" })).StatusCode);
            var before = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/history/" + subject))!;
            var wrong = Guid.NewGuid().ToString("D");
            Assert.Equal(HttpStatusCode.Conflict, (await http.GetAsync("/api/w1-development/clock/" + wrong)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/w1-development/command/" + wrong,
                new { operation = "correct", operationKey = "wrong", seconds = 123, basis = "synthetic" })).StatusCode);
            var after = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/history/" + subject))!;
            Assert.True(JsonNode.DeepEquals(before, after));
            var badPeriod = await http.PostAsJsonAsync("/api/w1-development/command/" + subject,
                new { operation = "period", operationKey = "invented", period = "invented-period" });
            Assert.Equal(HttpStatusCode.Conflict, badPeriod.StatusCode);
            // A real material transition, then externally signed head/chain closure.
            (await http.PostAsJsonAsync("/api/w1-development/command/" + subject,
                new { operation = "correct", operationKey = "correction", seconds = 120, basis = "synthetic qualified correction" })).EnsureSuccessStatusCode();
            var history = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/history/" + subject))!;
            var closure = (JsonObject)bundle.DeepClone();
            closure["identifier"] = "urn:amharc:development:observation-closure:" + subject;
            closure["currentMaterialHead"] = history["head"]!.DeepClone();
            closure["materialHistory"] = history["membership"]!.DeepClone(); Sign(closure);
            (await http.PostAsJsonAsync("/api/w1-development/closure/" + subject, closure)).EnsureSuccessStatusCode();
            var raw = await http.GetStringAsync("/api/w1-development/clock/" + subject);
            var envelope = JsonNode.Parse(raw)!;
            Assert.Equal("1", envelope["authority"]!["sequence"]!.GetValue<string>());
            Assert.EndsWith("Z", envelope["observedAtUtc"]!.GetValue<string>());
            Assert.Equal(120, envelope["clock"]!["accumulated"]!.GetValue<int>());
            Assert.Equal(subject, envelope["subject"]!["occurrenceId"]!.GetValue<string>());
            var dependencies = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/context"))!;
            // Capture's serialized HTTP response is exported unchanged for independent Node transport tests.
            var export = new JsonObject {
                ["subject"] = subject, ["governancePublicKey"] = publicPem, ["dependencies"] = dependencies,
                ["schema"] = JsonNode.Parse(File.ReadAllText(Path.Combine(W1ControlledVectorTests.DependenciesDirectory(),
                    "W1_DEPENDENCY_MANIFEST.json")))!["exactDependencies"]!["schema"]!.DeepClone(),
                ["rawEnvelope"] = raw,
                ["legacyRawEnvelope"] = JsonSerializer.Serialize(new ClockSnapshotV1(
                    "1.0", local, 1, 0, 0, false, 2659, DateTimeOffset.Parse("2026-10-04T10:00:00Z"),
                    new("Capture", "synthetic-legacy-instance"), 1, 1),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ["authorityGapHttp"] = new JsonObject { ["status"] = (int)gapResponse.StatusCode, ["raw"] = gapRaw },
                ["captureHttpEvidence"] = new JsonObject { ["concurrentRetries"] = 8, ["wrongSubject"] = "REFUSED",
                    ["wrongSubjectHistoryUnchanged"] = true, ["wrongSubjectSequenceUnconsumed"] = true,
                    ["unsupportedFormat"] = "REFUSED", ["authorityGap"] = "REFUSED", ["utcZ"] = true }
            };
            var derived = (JsonObject)envelope.DeepClone();
            derived["clock"]!["accumulated"] = 121; derived["clock"]!["periodElapsed"] = 121;
            var payloadDigest = W1CanonicalJson.Digest(derived.ToJsonString(), "attestation");
            derived["attestation"]!["payloadSha256"] = payloadDigest;
            derived["attestation"]!["signatureBase64"] = W1CanonicalJson.Sign(payloadDigest, seed);
            export["sameOrderDifferentPayload"] = derived.ToJsonString();
            var withoutAdvance = (JsonObject)envelope.DeepClone();
            withoutAdvance["clock"]!["state"] = "running";
            var runningDigest = W1CanonicalJson.Digest(withoutAdvance.ToJsonString(), "attestation");
            withoutAdvance["attestation"]!["payloadSha256"] = runningDigest;
            withoutAdvance["attestation"]!["signatureBase64"] = W1CanonicalJson.Sign(runningDigest, seed);
            export["runningWithoutAdvance"] = withoutAdvance.ToJsonString();
            var unknown = (JsonObject)envelope.DeepClone();
            unknown["context"]!["semantic"]!["sha256"] = new string('f', 64);
            var unknownDigest = W1CanonicalJson.Digest(unknown.ToJsonString(), "attestation");
            unknown["attestation"]!["payloadSha256"] = unknownDigest;
            unknown["attestation"]!["signatureBase64"] = W1CanonicalJson.Sign(unknownDigest, seed);
            export["unknownDependencyDigest"] = unknown.ToJsonString();
            var unsupported = (JsonObject)envelope.DeepClone();
            unsupported["clock"]!["periodKey"] = "invented-period";
            var unsupportedDigest = W1CanonicalJson.Digest(unsupported.ToJsonString(), "attestation");
            unsupported["attestation"]!["payloadSha256"] = unsupportedDigest;
            unsupported["attestation"]!["signatureBase64"] = W1CanonicalJson.Sign(unsupportedDigest, seed);
            export["unsupportedFormatPayload"] = unsupported.ToJsonString();
            export["captureHttpEvidence"]!["advanceWithoutProof"] = "REFUSED";
            export["captureHttpEvidence"]!["authorityConflict"] = "REFUSED";
            var conflict = (JsonObject)bundle.DeepClone();
            conflict["conflictingGrants"] = new JsonArray(bundle["grant"]!.DeepClone()); Sign(conflict);
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(
                "/api/w1-development/activate/" + subject + "/conflict", conflict)).StatusCode);
            var conflictResponse = await http.GetAsync("/api/w1-development/clock/" + subject);
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
            export["authorityConflictHttp"] = new JsonObject { ["status"] = (int)conflictResponse.StatusCode,
                ["raw"] = await conflictResponse.Content.ReadAsStringAsync() };
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(FindRoot())!, "capture-composed-output.json"), export.ToJsonString());
        }
        finally { await host.StopAsync(); CryptographicOperations.ZeroMemory(seed); Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public void W1HostDefaultDisabledOrNonDevelopment(string environment, bool enable)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["W1:DevelopmentOnly"] = enable.ToString() });
        builder.Services.AddW1DevelopmentComposition(builder.Configuration, builder.Environment);
        using var services = builder.Services.BuildServiceProvider();
        Assert.Null(services.GetService<W1DevelopmentApplication>());
    }

    [Fact]
    public void ExplicitEnablementWithoutDependenciesFailsClosed()
    {
        var c = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()).Build();
        Assert.Throws<InvalidOperationException>(() => new W1DevelopmentApplication(c));
    }
    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, ".git"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Genuine root missing");
    }
}