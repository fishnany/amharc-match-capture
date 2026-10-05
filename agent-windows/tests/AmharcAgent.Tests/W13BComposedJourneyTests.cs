using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AmharcAgent.Api.Controllers;
using AmharcAgent.Api.W1;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Tests;
using AmharcAgent.Infrastructure.Clock;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Xunit;

public sealed class W13BComposedJourneyTests
{
    private sealed class SyntheticTime : TimeProvider
    {
        private long _ticks;
        public void Advance(int seconds) => _ticks += seconds * TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-04T10:00:00Z");
    }
    [Fact]
    public async Task ActualSerializedHubOutboxCapture4x15CompleteObserverAndRecoveryRefusals()
    {
        var evidence = Environment.GetEnvironmentVariable("W13B_EVIDENCE_DIRECTORY")
            ?? throw new InvalidOperationException("Explicit isolated evidence directory required");
        Directory.CreateDirectory(evidence);
        var root = AmharcAgent.Tests.W1ControlledVectorTests.Root();
        var tagger = Path.Combine(Directory.GetParent(root)!.FullName, "candidate-tagger");
        var scratch = Path.Combine(Path.GetTempPath(), "w13b-actual-" + Guid.NewGuid());
        Directory.CreateDirectory(scratch);
        var dependencies = Path.Combine(scratch, "dependencies");
        foreach (var source in new[] {
            AmharcAgent.Tests.W1ControlledVectorTests.DependenciesDirectory(),
            Path.Combine(root, "governance/wave-1/match-setup/v1.0.0") })
        {
            var prefix = source.Contains("match-setup") ? "setup" : "clock";
            foreach (var file in Directory.GetFiles(source, "*.json", SearchOption.AllDirectories))
            {
                var target = Path.Combine(dependencies, prefix, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
            }
        }
        var seed = RandomNumberGenerator.GetBytes(32);
        var privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        string Pem(string label, byte[] bytes) => "-----BEGIN " + label + "-----\n" +
            Convert.ToBase64String(bytes) + "\n-----END " + label + "-----\n";
        var publicPem = Pem("PUBLIC KEY", SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(privateKey.GeneratePublicKey()).GetEncoded());
        File.WriteAllText(Path.Combine(scratch, "public.pem"), publicPem);
        File.WriteAllText(Path.Combine(scratch, "origin-private.pem"), Pem("PRIVATE KEY", PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey).GetEncoded()));
        File.WriteAllText(Path.Combine(scratch, "synthetic-seed"), Convert.ToBase64String(seed));
        var allocatorPath = Path.Combine(root, "governance/wave-1/match-setup/v1.0.0/fixtures/allocator-applicability.json");
        var allocator = JsonNode.Parse(File.ReadAllText(allocatorPath))!;
        var allocatorRef = new JsonObject { ["id"] = allocator["identifier"]!.DeepClone(), ["version"] = "1.0.0",
            ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(allocatorPath))).ToLowerInvariant() };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var journalPath = Path.Combine(scratch, "history.sqlite");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["W1:DevelopmentOnly"] = "true", ["W1:MatchSetupConformance"] = "true",
            ["W1:Issuer"] = "urn:amharc:test:capture:installation-a", ["W1:Actor"] = "synthetic-composed-actor",
            ["W1:Build"] = "isolated-w1-3b-candidate", ["W1:DependencyDirectory"] = dependencies,
            ["W1:ScratchJournalPath"] = journalPath, ["W1:GovernancePublicKeyFile"] = Path.Combine(scratch, "public.pem"),
            ["W1:DevelopmentSigningSeedFile"] = Path.Combine(scratch, "synthetic-seed"),
            ["W1:TaggerOriginIssuer"] = "urn:amharc:test:tagger:installation-a",
            ["W1:TaggerOriginPublicKeyFile"] = Path.Combine(scratch, "public.pem"),
            ["W1:SetupAllocatorApplicabilityRef"] = allocatorRef.ToJsonString()
        });
        var time = new SyntheticTime();
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddW1DevelopmentComposition(builder.Configuration, builder.Environment);
        builder.Services.AddControllers().AddApplicationPart(typeof(W1MatchSetupController).Assembly);
        await using var host = builder.Build();
        host.MapControllers();
        await host.StartAsync();
        var url = host.Urls.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        async Task Node(string phase)
        {
            var start = new ProcessStartInfo("pnpm") { WorkingDirectory = tagger, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in new[] { "--filter", "@workspace/shot-tagger-pro", "exec", "vitest", "run",
                "src/lib/__tests__/w13bComposedJourney.test.ts", "--reporter=json",
                "--outputFile=" + Path.Combine(evidence, "node-" + phase + ".json") }) start.ArgumentList.Add(arg);
            start.Environment["W13B_CAPTURE_URL"] = url;
            start.Environment["W13B_SCRATCH"] = scratch;
            start.Environment["W13B_PHASE"] = phase;
            start.Environment["W13B_NODE_RESULT"] = Path.Combine(evidence, phase + "-evidence.json");
            using var p = Process.Start(start)!;
            var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            File.WriteAllText(Path.Combine(evidence, "node-" + phase + ".log"), await stdout + await stderr);
            Assert.True(p.ExitCode == 0, "Actual Node " + phase + " failed; see isolated log/evidence");
        }
        async Task<JsonObject> Post(string path, JsonObject body)
        {
            var response = await http.PostAsJsonAsync(path, body);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, path + " HTTP " + response.StatusCode + " " + raw);
            return JsonNode.Parse(raw)!.AsObject();
        }
        void Sign(JsonObject body)
        {
            var digest = W1CanonicalJson.Digest(body.ToJsonString(), "governanceProof");
            body["governanceProof"] = new JsonObject { ["payloadSha256"] = digest,
                ["signatureBase64"] = W1CanonicalJson.Sign(digest, seed) };
        }
        try
        {
            await Node("deliver");
            var delivered = JsonNode.Parse(File.ReadAllText(Path.Combine(evidence, "deliver-evidence.json")))!;
            var subject = delivered["subject"]!.GetValue<string>();
            _ = new W1SqliteClockJournal(journalPath);
            var matrix = new JsonArray();
            async Task CheckRefusal(string name, string path, JsonObject body, string expectedCode)
            {
                var before = W13BGovernedStateEvidence.Snapshot(journalPath);
                var response = await http.PostAsJsonAsync(path, body);
                var raw = await response.Content.ReadAsStringAsync();
                var value = JsonNode.Parse(raw)!;
                var actualCode = value["code"]?.GetValue<string>() ?? value["failure"]?["reasonCode"]?.GetValue<string>();
                var after = W13BGovernedStateEvidence.Snapshot(journalPath);
                matrix.Add(new JsonObject { ["case"] = name, ["expected"] = expectedCode, ["actual"] = actualCode,
                    ["input"] = body.DeepClone(), ["httpStatus"] = (int)response.StatusCode,
                    ["response"] = value.DeepClone(), ["before"] = before, ["after"] = after });
                File.WriteAllText(Path.Combine(evidence, "actual-negative-matrix.json"), matrix.ToJsonString());
                Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal(expectedCode, actualCode);
                Assert.True(JsonNode.DeepEquals(before, after), name + " mutated governed persistence");
            }
            var frozen = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
                "governance/wave-1/match-setup/v1.0.0/W1_MATCH_FORMAT_HANDOFF_CONFORMANCE_VECTORS.json")))!["vectors"]!.AsArray();
            foreach (var number in new[] { 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 28 })
            {
                var vector = frozen[number - 1]!;
                var raw = vector["input"]!.ToJsonString();
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/w1/match-setups");
                request.Content = new StringContent(raw, System.Text.Encoding.UTF8, "application/json");
                request.Headers.Add("X-W1-Tagger-Issuer", "urn:amharc:test:tagger:installation-a");
                request.Headers.Add("X-W1-Origin-Signature", W1CanonicalJson.Sign(W1CanonicalJson.Digest(raw), seed));
                var before = W13BGovernedStateEvidence.Snapshot(journalPath);
                var response = await http.SendAsync(request);
                var value = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                var actual = value["failure"]?["reasonCode"]?.GetValue<string>();
                var expectedCode = vector["expected"]!["reasonCode"]?.GetValue<string>();
                var after = W13BGovernedStateEvidence.Snapshot(journalPath);
                matrix.Add(new JsonObject { ["case"] = vector["id"]!.DeepClone(), ["frozenInputReused"] = true,
                    ["scope"] = "Actual allocated product subject differs from synthetic vector allocator; reason/idempotency/no-mutation checks",
                    ["expected"] = expectedCode, ["actual"] = actual, ["input"] = vector["input"]!.DeepClone(),
                    ["response"] = value.DeepClone(), ["before"] = before, ["after"] = after });
                File.WriteAllText(Path.Combine(evidence, "actual-negative-matrix.json"), matrix.ToJsonString());
                Assert.Equal(expectedCode, actual);
                if (expectedCode is not null)
                {
                    Assert.Equal("REFUSED", value["status"]!.GetValue<string>());
                    Assert.True(JsonNode.DeepEquals(before, after));
                }
                else Assert.Equal("SUCCEEDED", value["status"]!.GetValue<string>());
                if (vector["expected"]!["stateMutationAllowed"]?.GetValue<bool>() == false)
                    Assert.True(JsonNode.DeepEquals(before, after));
                Assert.Equal(1, host.Services.GetRequiredService<W1MatchSetupApplication>().Ledger.Counts().Allocations);
            }
            // Separate actual signed receive composition for pre-activity
            // immutable correction; preserve the main 4x15 journey unchanged.
            var correctionConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(builder.Configuration.AsEnumerable())
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["W1:ScratchJournalPath"] = Path.Combine(scratch, "correction.sqlite")
                }).Build();
            var correctionApplication = new W1MatchSetupApplication(correctionConfiguration);
            var originalRequest = delivered["persistedRequestBytes"]!.GetValue<string>();
            var initial = correctionApplication.Receive(originalRequest, "urn:amharc:test:tagger:installation-a",
                W1CanonicalJson.Sign(W1CanonicalJson.Digest(originalRequest), seed));
            var beforeCorrection = correctionApplication.Ledger.Read(initial["canonicalOccurrenceId"]!.GetValue<string>())!;
            var correctedRequest = frozen[54]!["input"]!.DeepClone().AsObject();
            correctedRequest["intent"]!["expectedOccurrenceId"] = initial["canonicalOccurrenceId"]!.DeepClone();
            var correctedRaw = correctedRequest.ToJsonString();
            var correctedResult = correctionApplication.Receive(correctedRaw, "urn:amharc:test:tagger:installation-a",
                W1CanonicalJson.Sign(W1CanonicalJson.Digest(correctedRaw), seed));
            Assert.Equal("SUCCEEDED", correctedResult["status"]!.GetValue<string>());
            Assert.Equal(initial["canonicalOccurrenceId"]!.GetValue<string>(), correctedResult["canonicalOccurrenceId"]!.GetValue<string>());
            Assert.Equal(2, correctionApplication.Ledger.Counts().Revisions);
            var correctedSetup = correctionApplication.Ledger.Read(initial["canonicalOccurrenceId"]!.GetValue<string>())!;
            Assert.True(JsonNode.DeepEquals(correctedRequest["formatRef"], correctedSetup["formatRef"]));
            File.WriteAllText(Path.Combine(evidence, "actual-pre-activity-correction.json"),
                new JsonObject { ["before"] = beforeCorrection, ["request"] = correctedRequest,
                    ["result"] = correctedResult, ["after"] = correctedSetup,
                    ["scope"] = "Actual signed receive/durable ledger; not correction of an already prepared live runtime" }.ToJsonString());
            var prepare = await Post("/api/w1/match-setups/" + subject + "/prepare", new JsonObject());
            var grant = JsonNode.Parse(File.ReadAllText(Path.Combine(
                AmharcAgent.Tests.W1ControlledVectorTests.DependenciesDirectory(), "fixtures/trustBundle.json")))!.AsObject();
            grant["identifier"] = "urn:amharc:test:composed:grant:" + subject;
            grant["closure"]!["subject"] = subject; grant["grant"]!["subject"] = subject;
            grant["grant"]!["incarnationId"] = prepare["incarnationId"]!.DeepClone();
            grant["grant"]!["capabilities"] = new JsonArray("snapshot", "advance", "pause-resume", "correct", "checkpoint", "recover", "period-transition");
            grant["resolutionRef"] = prepare["resolutionRef"]!.DeepClone(); grant["principalPublicKey"] = publicPem;
            Sign(grant);
            Assert.Equal(System.Net.HttpStatusCode.Conflict, (await http.GetAsync("/api/w1-development/clock/" + subject)).StatusCode);
            await Post("/api/w1-development/activate/" + subject + "/synthetic-activation", grant);
            var correction = frozen[55]!["input"]!.DeepClone().AsObject();
            correction["intent"]!["expectedOccurrenceId"] = subject;
            http.DefaultRequestHeaders.Add("X-W1-Tagger-Issuer", "urn:amharc:test:tagger:installation-a");
            http.DefaultRequestHeaders.Add("X-W1-Origin-Signature",
                W1CanonicalJson.Sign(W1CanonicalJson.Digest(correction.ToJsonString()), seed));
            await CheckRefusal("post-zero-time-activation-format-correction",
                "/api/w1/match-setups", correction, "FORMAT_LOCKED_AFTER_ACTIVITY");
            http.DefaultRequestHeaders.Remove("X-W1-Origin-Signature");
            http.DefaultRequestHeaders.Remove("X-W1-Tagger-Issuer");
            foreach (var pair in new[] {
                ("skip-period", "period", "quarter-3", "WRONG_SUCCESSOR"),
                ("repeat-period", "period", "quarter-1", "WRONG_SUCCESSOR"),
                ("inapplicable-et", "period", "extra-time-1", "ET_NOT_APPLICABLE"),
                ("wrong-completion", "complete", (string?)null, "WRONG_COMPLETION") })
                await CheckRefusal(pair.Item1, "/api/w1-development/command/" + subject,
                    new JsonObject { ["operation"] = pair.Item2, ["period"] = pair.Item3,
                        ["operationKey"] = Guid.NewGuid().ToString() }, pair.Item4);
            await CheckRefusal("wrong-subject", "/api/w1-development/command/" + Guid.NewGuid(),
                new JsonObject { ["operation"] = "pause", ["operationKey"] = Guid.NewGuid().ToString() }, "W1_SUBJECT_REFUSAL");
            async Task Command(string operation, string? period = null) => await Post("/api/w1-development/command/" + subject,
                new JsonObject { ["operation"] = operation, ["operationKey"] = Guid.NewGuid().ToString(),
                    ["period"] = period, ["basis"] = "synthetic controlled period flow" });
            var stages = new JsonArray();
            for (var i = 1; i <= 4; i++)
            {
                await Command("resume"); time.Advance(900); await Command("tick"); await Command("pause");
                stages.Add(new JsonObject { ["quarter"] = i, ["accumulated"] = i * 900 });
                if (i < 4) await Command("period", "quarter-" + (i + 1));
                if (i == 1) await CheckRefusal("backtrack-period", "/api/w1-development/command/" + subject,
                    new JsonObject { ["operation"] = "period", ["period"] = "quarter-1",
                        ["operationKey"] = Guid.NewGuid().ToString() }, "WRONG_SUCCESSOR");
            }
            await Command("complete");
            var history = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/history/" + subject))!;
            var closure = (JsonObject)grant.DeepClone();
            closure["identifier"] = "urn:amharc:test:composed:observation:" + subject;
            closure["currentMaterialHead"] = history["head"]!.DeepClone();
            closure["materialHistory"] = history["membership"]!.DeepClone(); Sign(closure);
            await Post("/api/w1-development/closure/" + subject, closure);
            var rawEnvelope = await http.GetStringAsync("/api/w1-development/clock/" + subject);
            var envelope = JsonNode.Parse(rawEnvelope)!;
            Assert.Equal(3600, envelope["clock"]!["accumulated"]!.GetValue<int>());
            Assert.Equal("quarter-4", envelope["clock"]!["periodKey"]!.GetValue<string>());
            Assert.Equal("completed", envelope["clock"]!["phase"]!.GetValue<string>());
            var exported = JsonNode.Parse(await http.GetStringAsync("/api/w1-development/context"))!;
            var controlled = JsonNode.Parse(File.ReadAllText(Path.Combine(
                AmharcAgent.Tests.W1ControlledVectorTests.DependenciesDirectory(), "W1_DEPENDENCY_MANIFEST.json")))!;
            var schema = controlled["exactDependencies"]!["schema"]!;
            var observerInput = new JsonObject { ["subject"] = subject, ["schema"] = schema.DeepClone(),
                ["governancePublicKey"] = publicPem, ["dependencies"] = exported.DeepClone(), ["rawEnvelope"] = rawEnvelope };
            File.WriteAllText(Path.Combine(scratch, "observer-input.json"), observerInput.ToJsonString());
            File.WriteAllText(Path.Combine(evidence, "actual-signed-observer-input.json"), observerInput.ToJsonString());
            await Node("observe");
            File.WriteAllText(Path.Combine(evidence, "positive-runtime-stages.json"), stages.ToJsonString());
            var journal = new W1SqliteClockJournal(journalPath);
            var stored = journal.Read(subject)!;
            void SeedCheckpoint(JsonObject checkpoint)
            {
                using var db = new SqliteConnection("Data Source=" + journalPath);
                db.Open(); using var command = db.CreateCommand();
                command.CommandText = "UPDATE W1ClockContexts SET ContextJson=$json WHERE Subject=$subject";
                command.Parameters.AddWithValue("$json", checkpoint.ToJsonString());
                command.Parameters.AddWithValue("$subject", subject); Assert.Equal(1, command.ExecuteNonQuery());
            }
            foreach (var number in new[] { 58, 59, 60 })
            {
                var checkpoint = JsonNode.Parse(stored.ContextJson)!.AsObject();
                checkpoint["matchSetup"]!["formatRef"] = frozen[number - 1]!["input"]!["formatRef"]!.DeepClone();
                if (number == 60)
                {
                    var input = frozen[number - 1]!["input"]!;
                    checkpoint["clock"]!["periodKey"] = input["currentPeriodKey"]!.DeepClone();
                    checkpoint["clock"]!["periodChain"] = input["periodChain"]!.DeepClone();
                    checkpoint["clock"]!["accumulated"] = input["accumulatedSeconds"]!.DeepClone();
                    checkpoint["clock"]!["periodStart"] = input["periodStartSeconds"]!.DeepClone();
                    checkpoint["clock"]!["periodElapsed"] = input["periodElapsedSeconds"]!.DeepClone();
                    checkpoint["clock"]!["phase"] = "playing";
                }
                SeedCheckpoint(checkpoint); // Explicitly seeded corrupt synthetic fixture, BEFORE refusal snapshot.
                // The directly overwritten period/checkpoint no longer matches
                // its immutable material head. Preserve this distinct safeguard
                // case; it is NOT the relationship-consistent frozen vector 60.
                await CheckRefusal(number == 60 ? "actual-persisted-multi-fault-material-integrity" :
                        "actual-persisted-" + frozen[number - 1]!["id"]!.GetValue<string>(),
                    "/api/w1-development/command/" + subject,
                    new JsonObject { ["operation"] = "recover", ["operationKey"] = Guid.NewGuid().ToString() },
                    number == 60 ? "W1_RECOVERY_RELATIONSHIP_REFUSAL" :
                        frozen[number - 1]!["expected"]!["reasonCode"]!.GetValue<string>());
            }
            // Invoke the accepted atomic-append fixture and its valid control
            // for actual relationship-consistent vector-60 Restore coverage.
            // Its assertions preserve exact closure/material relationships and
            // durable AND in-memory no-mutation on RECOVERY_PERIOD_INVALID.
            AmharcAgent.Tests.W13BSingleFaultRecoveryTests.RunRelationshipConsistentRecoveryFixture(
                root, Path.Combine(evidence, "composed-vector-60-single-fault"));
            SeedCheckpoint(JsonNode.Parse(stored.ContextJson)!.AsObject());
            // Restore an independently constructed composition/incarnation using
            // the exact exported local closure, never an ID/default fallback.
            foreach (var member in exported.AsArray())
            {
                var bytes = Convert.FromBase64String(member!["bytesBase64"]!.GetValue<string>());
                var path = Path.Combine(dependencies, "restored", W1MatchSetupAdmission.Text(member["reference"]!["sha256"]) + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
            }
            var restarted = new W1DevelopmentApplication(builder.Configuration, time);
            var setups = host.Services.GetRequiredService<W1MatchSetupApplication>();
            var preparedAgain = System.Text.Json.JsonSerializer.SerializeToNode(
                restarted.PrepareMatchSetup(setups.Ledger.Read(subject)!, setups.Ledger.Closure()),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            Assert.NotEqual(prepare["incarnationId"]!.GetValue<string>(), preparedAgain["incarnationId"]!.GetValue<string>());
            var newGrant = (JsonObject)grant.DeepClone();
            newGrant["identifier"] = "urn:amharc:test:composed:new-incarnation-grant:" + subject;
            newGrant["grant"]!["incarnationId"] = preparedAgain["incarnationId"]!.DeepClone();
            newGrant["resolutionRef"] = preparedAgain["resolutionRef"]!.DeepClone(); Sign(newGrant);
            restarted.Activate(subject, newGrant, "new-incarnation-activation");
            restarted.Command(subject, "recover", "new-incarnation-qualified-recovery");
            var recoveredHistory = System.Text.Json.JsonSerializer.SerializeToNode(restarted.History(subject))!;
            var recoveredClosure = (JsonObject)newGrant.DeepClone();
            recoveredClosure["identifier"] = "urn:amharc:test:composed:recovered-closure:" + subject;
            recoveredClosure["currentMaterialHead"] = recoveredHistory["head"]!.DeepClone();
            recoveredClosure["materialHistory"] = recoveredHistory["membership"]!.DeepClone(); Sign(recoveredClosure);
            restarted.InstallObservationClosure(subject, recoveredClosure);
            var recovered = restarted.Observe(subject);
            Assert.Equal(3600, recovered["clock"]!["accumulated"]!.GetValue<int>());
            Assert.Equal("paused", recovered["clock"]!["state"]!.GetValue<string>());
            Assert.Equal("utc-reconstructed", recovered["clock"]!["method"]!.GetValue<string>());
            File.WriteAllText(Path.Combine(evidence, "actual-new-incarnation-recovery.json"), recovered.ToJsonString());
            var conflicting = (JsonObject)grant.DeepClone();
            conflicting["conflictingGrants"] = new JsonArray(new JsonObject { ["principalId"] = "synthetic-competing-principal" });
            Sign(conflicting);
            await CheckRefusal("authority-conflict", "/api/w1-development/activate/" + subject + "/conflicting-scope",
                conflicting, "W1_AUTHORITY_CONFLICT");
        }
        finally
        {
            await host.StopAsync(); CryptographicOperations.ZeroMemory(seed);
            SqliteConnection.ClearAllPools(); Directory.Delete(scratch, true);
        }
    }
}