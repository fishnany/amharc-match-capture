using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AmharcAgent.Api.Controllers;
using AmharcAgent.Api.W1;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class W13CInstalledAssuranceFixtureTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-04T10:00:00Z");
    }
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Theory]
    [InlineData("Production", true, true, true)]
    [InlineData("Development", false, true, true)]
    [InlineData("Development", true, false, true)]
    [InlineData("Development", true, true, false)]
    public async Task FixtureRouteIsUnavailableUnlessEveryIndependentGuardIsEnabled(
        string environment, bool developmentOnly, bool matchSetup, bool fixtures)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["W1:DevelopmentOnly"] = developmentOnly.ToString(),
            ["W1:MatchSetupConformance"] = matchSetup.ToString(),
            ["W1:InstalledAssuranceFixtures"] = fixtures.ToString()
        });
        builder.Services.AddW1DevelopmentComposition(builder.Configuration, builder.Environment);
        builder.Services.AddControllers().AddApplicationPart(typeof(W1InstalledAssuranceFixtureController).Assembly);
        await using var host = builder.Build();
        host.MapControllers();
        await host.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
        var response = await http.PostAsJsonAsync("/api/w1-development/installed-assurance-fixtures/synthetic",
            new { scenario = "recovery-current-period-outside-format", operationKey = "blocked" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("recovery-unresolved-format", "RECOVERY_FORMAT_UNRESOLVED")]
    [InlineData("recovery-current-period-outside-format", "RECOVERY_PERIOD_INVALID")]
    public async Task RealHttpFixtureThenNewIncarnationActualRestoreRefusesWithoutGovernedMutation(
        string scenario, string expected)
    {
        var root = W1ControlledVectorTests.Root();
        var scratch = Path.Combine(Path.GetTempPath(), "w13c-installed-fixtures-" + Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(scratch);
        var seed = RandomNumberGenerator.GetBytes(32);
        try
        {
            var key = new Ed25519PrivateKeyParameters(seed, 0);
            var publicPem = "-----BEGIN PUBLIC KEY-----\n" +
                Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                    key.GeneratePublicKey()).GetEncoded()) + "\n-----END PUBLIC KEY-----\n";
            File.WriteAllText(Path.Combine(scratch, "public.pem"), publicPem);
            var closure = Path.Combine(scratch, "closure");
            foreach (var source in new[] { "governance/wave-1/v1.0.0",
                "governance/wave-1/match-setup/v1.0.0" })
            foreach (var file in Directory.GetFiles(Path.Combine(root, source), "*.json", SearchOption.AllDirectories))
            {
                var target = Path.Combine(closure, source, Path.GetRelativePath(Path.Combine(root, source), file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            var allocatorPath = Path.Combine(root,
                "governance/wave-1/match-setup/v1.0.0/fixtures/allocator-applicability.json");
            var allocator = JsonNode.Parse(File.ReadAllText(allocatorPath))!;
            var allocatorRef = new JsonObject {
                ["id"] = allocator["identifier"]!.DeepClone(), ["version"] = allocator["version"]!.DeepClone(),
                ["sha256"] = Hash(File.ReadAllBytes(allocatorPath))
            };
            var database = Path.Combine(scratch, "synthetic.sqlite");
            var values = new Dictionary<string, string?> {
                ["W1:DevelopmentOnly"] = "true", ["W1:MatchSetupConformance"] = "true",
                ["W1:InstalledAssuranceFixtures"] = "true",
                ["W1:Issuer"] = "urn:amharc:test:capture:installation-a",
                ["W1:Actor"] = "TEST_ONLY-installed-assurance",
                ["W1:Build"] = "isolated-candidate",
                ["W1:DependencyDirectory"] = closure, ["W1:ScratchJournalPath"] = database,
                ["W1:GovernancePublicKeyFile"] = Path.Combine(scratch, "public.pem"),
                ["W1:TaggerOriginIssuer"] = "urn:amharc:test:tagger:installation-a",
                ["W1:TaggerOriginPublicKeyFile"] = Path.Combine(scratch, "public.pem"),
                ["W1:SetupAllocatorApplicabilityRef"] = allocatorRef.ToJsonString()
            };
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(values);
            builder.Services.AddSingleton<TimeProvider>(new FixedTime());
            builder.Services.AddW1DevelopmentComposition(builder.Configuration, builder.Environment);
            builder.Services.AddControllers().AddApplicationPart(typeof(W1InstalledAssuranceFixtureController).Assembly);
            await using var host = builder.Build();
            host.MapControllers();
            await host.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
            var setups = host.Services.GetRequiredService<W1MatchSetupApplication>();
            var app = host.Services.GetRequiredService<W1DevelopmentApplication>();
            var vectors = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
                "governance/wave-1/match-setup/v1.0.0/W1_MATCH_FORMAT_HANDOFF_CONFORMANCE_VECTORS.json")))!
                ["vectors"]!;
            var raw = vectors[11]!["input"]!.ToJsonString();
            var signed = W1CanonicalJson.Sign(W1CanonicalJson.Digest(raw), seed);
            var receipt = setups.Receive(raw, "urn:amharc:test:tagger:installation-a", signed);
            Assert.Equal("SUCCEEDED", receipt["status"]!.GetValue<string>());
            var subject = receipt["canonicalOccurrenceId"]!.GetValue<string>();
            var prepared = JsonSerializer.SerializeToNode(
                app.PrepareMatchSetup(setups.Ledger.Read(subject)!, setups.Ledger.Closure()), Wire)!;
            JsonObject Grant(JsonNode preparation, string suffix)
            {
                var grant = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
                    "governance/wave-1/v1.0.0/fixtures/trustBundle.json")))!.AsObject();
                grant["identifier"] = "urn:amharc:test:installed-assurance:" + suffix + ":" + subject;
                grant["closure"]!["subject"] = subject;
                grant["grant"]!["subject"] = subject;
                grant["grant"]!["incarnationId"] = preparation["incarnationId"]!.DeepClone();
                grant["grant"]!["capabilities"] = new JsonArray("snapshot", "correct", "checkpoint",
                    "recover", "period-transition", "pause-resume", "advance");
                grant["resolutionRef"] = preparation["resolutionRef"]!.DeepClone();
                grant["principalPublicKey"] = publicPem;
                var digest = W1CanonicalJson.Digest(grant.ToJsonString(), "governanceProof");
                grant["governanceProof"] = new JsonObject { ["payloadSha256"] = digest,
                    ["signatureBase64"] = W1CanonicalJson.Sign(digest, seed) };
                return grant;
            }
            app.Activate(subject, Grant(prepared, "initial"), "TEST_ONLY-activate-initial");
            app.Command(subject, "correct", "TEST_ONLY-935", 935, basis: "TEST_ONLY setup");
            var endpoint = "/api/w1-development/installed-assurance-fixtures/" + subject;
            var initial = W13BGovernedStateEvidence.Snapshot(database);
            var bad = await http.PostAsJsonAsync(endpoint, new { scenario = "unsupported", operationKey = "bad" });
            Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
            Assert.Contains("W1_ASSURANCE_SCENARIO_REFUSAL", await bad.Content.ReadAsStringAsync());
            var extra = await http.PostAsJsonAsync(endpoint, new {
                scenario, operationKey = "bad-extra", overrideCheckpoint = "not allowed"
            });
            Assert.Equal(HttpStatusCode.Conflict, extra.StatusCode);
            Assert.Contains("W1_ASSURANCE_SCENARIO_REFUSAL", await extra.Content.ReadAsStringAsync());
            var nonSynthetic = await http.PostAsJsonAsync(
                "/api/w1-development/installed-assurance-fixtures/not-a-synthetic-subject",
                new { scenario, operationKey = "bad-subject" });
            Assert.Equal(HttpStatusCode.Conflict, nonSynthetic.StatusCode);
            Assert.True(JsonNode.DeepEquals(initial, W13BGovernedStateEvidence.Snapshot(database)));

            var fixtureKey = "TEST_ONLY-installed-fixture";
            async Task<JsonNode> Fixture(string kind, string opKey, HttpStatusCode status)
            {
                var response = await http.PostAsJsonAsync(endpoint, new { scenario = kind, operationKey = opKey });
                Assert.Equal(status, response.StatusCode);
                return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            }
            var result = await Fixture(scenario, fixtureKey, HttpStatusCode.OK);
            Assert.Equal(scenario, result["scenario"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(result, await Fixture(scenario, fixtureKey, HttpStatusCode.OK)));
            var afterFixture = W13BGovernedStateEvidence.Snapshot(database);
            var conflicting = await Fixture(scenario == "recovery-unresolved-format"
                ? "recovery-current-period-outside-format" : "recovery-unresolved-format",
                fixtureKey, HttpStatusCode.Conflict);
            Assert.Equal("W1_ASSURANCE_FIXTURE_CONFLICT", conflicting["code"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(afterFixture, W13BGovernedStateEvidence.Snapshot(database)));
            Assert.Throws<InvalidOperationException>(() =>
                app.Command(subject, "checkpoint", "must-restart", basis: "TEST_ONLY"));

            // Preserve the exact public historical dependencies across the new
            // process. Never copy private key material or remove controlled files.
            var exported = JsonSerializer.SerializeToNode(app.Dependencies(), Wire)!.AsArray();
            foreach (var member in exported)
            {
                var bytes = Convert.FromBase64String(member!["bytesBase64"]!.GetValue<string>());
                var file = Path.Combine(closure, "dynamic", Hash(bytes) + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, bytes);
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var restarted = new W1DevelopmentApplication(configuration, new FixedTime());
            var reloaded = new W1MatchSetupApplication(configuration);
            var fresh = JsonSerializer.SerializeToNode(
                restarted.PrepareMatchSetup(reloaded.Ledger.Read(subject)!, reloaded.Ledger.Closure()), Wire)!;
            Assert.NotEqual(prepared["incarnationId"]!.GetValue<string>(), fresh["incarnationId"]!.GetValue<string>());
            restarted.Activate(subject, Grant(fresh, "new-incarnation"), "TEST_ONLY-activate-recovery");
            var beforeRecovery = W13BGovernedStateEvidence.Snapshot(database);
            var refusal = Assert.Throws<InvalidOperationException>(() =>
                restarted.Command(subject, "recover", "TEST_ONLY-recover"));
            Assert.Equal(expected, refusal.Message);
            var afterRecovery = W13BGovernedStateEvidence.Snapshot(database);
            Assert.True(JsonNode.DeepEquals(beforeRecovery, afterRecovery));
            Assert.Equal(1, reloaded.Ledger.Counts().Allocations);
            if (Environment.GetEnvironmentVariable("W13C_ASSURANCE_EVIDENCE_DIRECTORY") is { Length: > 0 } evidence)
            {
                Directory.CreateDirectory(evidence);
                var record = new JsonObject {
                    ["standing"] = "TEST_ONLY / LINUX APPLICATION-CANDIDATE / NOT WINDOWS INSTALLED ASSURANCE",
                    ["boundary"] = "Real HTTP fixture route, then actual W1DevelopmentApplication.Command -> W1SubjectBoundRuntime.Restore",
                    ["scenario"] = scenario, ["subject"] = subject,
                    ["fixtureReceipt"] = result.DeepClone(),
                    ["preparationBefore"] = initial.DeepClone(),
                    ["preparationAfter"] = afterFixture.DeepClone(),
                    ["recoveryBefore"] = beforeRecovery.DeepClone(),
                    ["recoveryAfter"] = afterRecovery.DeepClone(),
                    ["actualRecoveryRefusal"] = refusal.Message,
                    ["recoveryNoGovernedMutation"] = true, ["allocations"] = 1,
                    ["differentIncarnation"] = true, ["duplicateFixtureIdempotent"] = true,
                    ["conflictingOperationRefused"] = true
                };
                File.WriteAllText(Path.Combine(evidence, scenario + "-" + Guid.NewGuid().ToString("D") + ".json"),
                    record.ToJsonString(Wire));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(scratch, recursive: true);
        }
    }
}
