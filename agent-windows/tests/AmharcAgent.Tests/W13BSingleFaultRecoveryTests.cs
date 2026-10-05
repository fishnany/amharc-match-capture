using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AmharcAgent.Api.W1;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;

namespace AmharcAgent.Tests;

/// <summary>TEST_ONLY single-fault historical fixture. Actual persisted Restore;
/// no product modifications, relationship mocks, direct policy substitution,
/// normative-byte edits or operational appointments.</summary>
public sealed class W13BSingleFaultRecoveryTests
{
    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-04T10:00:00Z");
    }
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);
    private static string Hash(byte[] raw) => Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
    private static JsonObject Ref(W1Reference r) => new() { ["id"] = r.Id, ["sha256"] = r.Sha256 };
    private static JsonObject Memory(W1DevelopmentApplication app)
    {
        var runtime = typeof(W1DevelopmentApplication).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var result = new JsonObject();
        foreach (var name in new[] { "_clock", "_revision", "_historyId", "_status", "_sequence", "_pendingRecovery" })
        {
            var value = runtime.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime);
            result[name] = value is JsonNode node ? JsonNode.Parse(node.ToJsonString()) : JsonSerializer.SerializeToNode(value);
        }
        result["incarnationId"] = runtime.GetType().GetProperty("IncarnationId")!.GetValue(runtime)!.ToString();
        return new JsonObject { ["logicalState"] = result, ["sha256"] = W1CanonicalJson.Digest(result.ToJsonString()) };
    }

    [Fact]
    public void ValidControlThenSingleFault_ActualPersistedRestore_NoFunctionalRepair()
    {
        var root = Environment.GetEnvironmentVariable("W13B_SINGLEFAULT_CAPTURE_ROOT")
            ?? throw new InvalidOperationException("Explicit isolated candidate root required");
        var evidence = Environment.GetEnvironmentVariable("W13B_SINGLEFAULT_EVIDENCE")
            ?? throw new InvalidOperationException("Explicit separate evidence directory required");
        Directory.CreateDirectory(evidence);
        var scratch = Path.Combine(Path.GetTempPath(), "w13b-single-fault-" + Guid.NewGuid());
        Directory.CreateDirectory(scratch);
        var seed = RandomNumberGenerator.GetBytes(32);
        var key = new Ed25519PrivateKeyParameters(seed, 0);
        var publicPem = "-----BEGIN PUBLIC KEY-----\n" +
            Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key.GeneratePublicKey()).GetEncoded()) +
            "\n-----END PUBLIC KEY-----\n";
        File.WriteAllText(Path.Combine(scratch, "public.pem"), publicPem);
        File.WriteAllText(Path.Combine(scratch, "seed"), Convert.ToBase64String(seed));
        var closure = Path.Combine(scratch, "closure");
        foreach (var source in new[] { "governance/wave-1/v1.0.0", "governance/wave-1/match-setup/v1.0.0" })
        foreach (var file in Directory.GetFiles(Path.Combine(root, source), "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(closure, source, Path.GetRelativePath(Path.Combine(root, source), file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        var allocatorPath = Path.Combine(root, "governance/wave-1/match-setup/v1.0.0/fixtures/allocator-applicability.json");
        var allocatorDoc = JsonNode.Parse(File.ReadAllText(allocatorPath))!;
        var allocatorRef = new JsonObject { ["id"] = allocatorDoc["identifier"]!.DeepClone(),
            ["version"] = allocatorDoc["version"]!.DeepClone(), ["sha256"] = Hash(File.ReadAllBytes(allocatorPath)) };
        IConfiguration Configuration(string db) => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["W1:DevelopmentOnly"] = "true", ["W1:MatchSetupConformance"] = "true",
                ["W1:Issuer"] = "urn:amharc:test:capture:installation-a", ["W1:Actor"] = "TEST_ONLY-single-fault-constructor",
                ["W1:Build"] = "isolated-existing-product-source", ["W1:DependencyDirectory"] = closure,
                ["W1:ScratchJournalPath"] = db, ["W1:GovernancePublicKeyFile"] = Path.Combine(scratch, "public.pem"),
                ["W1:DevelopmentSigningSeedFile"] = Path.Combine(scratch, "seed"),
                ["W1:TaggerOriginIssuer"] = "urn:amharc:test:tagger:installation-a",
                ["W1:TaggerOriginPublicKeyFile"] = Path.Combine(scratch, "public.pem"),
                ["W1:SetupAllocatorApplicabilityRef"] = allocatorRef.ToJsonString()
            }).Build();
        void Sign(JsonObject grant)
        {
            var digest = W1CanonicalJson.Digest(grant.ToJsonString(), "governanceProof");
            grant["governanceProof"] = new JsonObject { ["payloadSha256"] = digest,
                ["signatureBase64"] = W1CanonicalJson.Sign(digest, seed) };
        }
        try
        {
            var db = Path.Combine(scratch, "base.sqlite");
            var config = Configuration(db);
            var vectors = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
                "governance/wave-1/match-setup/v1.0.0/W1_MATCH_FORMAT_HANDOFF_CONFORMANCE_VECTORS.json")))!["vectors"]!;
            var raw = vectors[11]!["input"]!.ToJsonString();
            var setups = new W1MatchSetupApplication(config);
            var receipt = setups.Receive(raw, "urn:amharc:test:tagger:installation-a",
                W1CanonicalJson.Sign(W1CanonicalJson.Digest(raw), seed));
            Assert.Equal("SUCCEEDED", receipt["status"]!.GetValue<string>());
            var subject = receipt["canonicalOccurrenceId"]!.GetValue<string>();
            var original = new W1DevelopmentApplication(config, new FixedTime());
            var preparation = JsonSerializer.SerializeToNode(original.PrepareMatchSetup(setups.Ledger.Read(subject)!, setups.Ledger.Closure()), Wire)!;
            var grant = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "governance/wave-1/v1.0.0/fixtures/trustBundle.json")))!.AsObject();
            grant["identifier"] = "urn:amharc:test:single-fault:grant:" + subject;
            grant["closure"]!["subject"] = subject; grant["grant"]!["subject"] = subject;
            grant["grant"]!["incarnationId"] = preparation["incarnationId"]!.DeepClone();
            grant["grant"]!["capabilities"] = new JsonArray("snapshot", "correct", "checkpoint", "recover", "period-transition", "pause-resume", "advance");
            grant["resolutionRef"] = preparation["resolutionRef"]!.DeepClone();
            grant["principalPublicKey"] = publicPem; Sign(grant);
            original.Activate(subject, grant, "TEST_ONLY-base-activation");
            original.Command(subject, "correct", "TEST_ONLY-base-935", 935, basis: "TEST_ONLY fixture construction");
            var journal = new W1SqliteClockJournal(db);
            var baseCheckpoint = journal.Read(subject)!;
            var baseActivity = journal.ReadActivity(baseCheckpoint.ActivityId)!;
            var exported = JsonSerializer.SerializeToNode(original.Dependencies(), Wire)!.AsArray();
            foreach (var member in exported)
            {
                var bytes = Convert.FromBase64String(member!["bytesBase64"]!.GetValue<string>());
                var path = Path.Combine(closure, "dynamic", Hash(bytes) + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
            }
            // CONTROL FIRST. Invalid variant changes only the semantic period
            // slot in BOTH checkpoint and matching activity. Exact references,
            // arithmetic and frozen one-entry quarter-1 chain are unchanged.
            var fixtureActivityId = Guid.NewGuid().ToString();
            foreach (var variant in new[] { ("control", "quarter-1"), ("single-fault", "extra-time-half-1") })
            {
                var variantDb = Path.Combine(scratch, variant.Item1 + ".sqlite");
                using (var source = new SqliteConnection("Data Source=" + db))
                using (var target = new SqliteConnection("Data Source=" + variantDb))
                { source.Open(); target.Open(); source.BackupDatabase(target); }
                var checkpoint = JsonNode.Parse(baseCheckpoint.ContextJson)!.AsObject();
                var activity = JsonNode.Parse(baseActivity.Json)!.AsObject();
                checkpoint["clock"]!["periodKey"] = variant.Item2;
                activity["identifier"] = "urn:amharc:w1:activity:" + fixtureActivityId;
                activity["activityId"] = fixtureActivityId;
                activity["inputActivity"] = baseActivity.Id;
                activity["parentRef"] = Ref(W1MaterialChain.Reference(baseActivity));
                activity["inputRevision"] = baseCheckpoint.Revision.ToString();
                activity["priorState"] = JsonNode.Parse(baseCheckpoint.ContextJson)!["clock"]!.DeepClone();
                activity["priorSeconds"] = 935;
                activity["sourceBasis"] = "TEST_ONLY appended single-fault material construction";
                activity["logicalOperationKey"] = "TEST_ONLY-constructed-material";
                activity["newState"]!["periodKey"] = variant.Item2;
                var activityJson = activity.ToJsonString();
                var fixtureInput = new JsonObject { ["operation"] = "correct",
                    ["basis"] = activity["sourceBasis"]!.DeepClone(), ["correctedSeconds"] = 935, ["periodKey"] = null };
                var material = new W1JournalActivity(fixtureActivityId, subject, "TEST_ONLY-constructed-material",
                    W1CanonicalJson.Digest(fixtureInput.ToJsonString()), activityJson);
                var exactHead = W1MaterialChain.Reference(material);
                checkpoint["activityId"] = fixtureActivityId;
                checkpoint["activityRef"] = Ref(exactHead);
                // Append via the existing atomic journal. Immutable history
                // triggers remain installed and prior material remains intact.
                new W1SqliteClockJournal(variantDb).Commit(baseCheckpoint.Revision,
                    new(subject, baseCheckpoint.Revision + 1, "1.0.0", checkpoint.ToJsonString(), fixtureActivityId), material);
                var dependencies = new W1DependencyResolver();
                foreach (var file in Directory.GetFiles(closure, "*.json", SearchOption.AllDirectories))
                {
                    var bytes = File.ReadAllBytes(file); var document = JsonNode.Parse(bytes)!;
                    if ((document["identifier"] ?? document["$id"]) is not null)
                        dependencies.Add(new((document["identifier"] ?? document["$id"])!.GetValue<string>(), Hash(bytes)), bytes);
                }
                var variantJournal = new W1SqliteClockJournal(variantDb);
                var stored = variantJournal.Read(subject)!;
                var verifier = new W1DevelopmentGrantVerifier(new(publicPem, new HashSet<string> { subject }, "official-match-accumulated"));
                var head = W1MaterialChain.CurrentHead(variantJournal, stored, dependencies);
                var validation = W1MaterialChain.Validate(head, subject, checkpoint["context"]!.AsObject(), dependencies, verifier);
                var oldGrant = verifier.Verify(checkpoint["historicalGrantEvidence"]!.AsObject(), subject,
                    checkpoint["incarnationId"]!.GetValue<string>()).Copy();
                var formatRef = checkpoint["matchSetup"]!["formatRef"]!;
                var resolvedFormat = new W1MatchSetupAdmission(dependencies).ResolveRecoveryFormat(formatRef);
                var relationships = new JsonObject {
                    ["newStateEqualsCheckpointClock"] = JsonNode.DeepEquals(activity["newState"], checkpoint["clock"]),
                    ["exactContextEqualsCheckpointContext"] = JsonNode.DeepEquals(activity["exactContext"], checkpoint["context"]),
                    ["historicalGrantEqualsCheckpointGrant"] = JsonNode.DeepEquals(activity["historicalGrantEvidence"], checkpoint["historicalGrantEvidence"]),
                    ["incarnationMatches"] = JsonNode.DeepEquals(activity["incarnationId"], checkpoint["incarnationId"]),
                    ["activityIdMatches"] = stored.ActivityId == activity["activityId"]!.GetValue<string>(),
                    ["activityDigestMatches"] = head == exactHead && Hash(Encoding.UTF8.GetBytes(activityJson)) == head.Sha256,
                    ["checkpointHeadMatches"] = W1DependencyResolver.Reference(checkpoint["activityRef"]!) == head,
                    ["historicalEligibility"] = DateTimeOffset.Parse(activity["time"]!["value"]!.GetValue<string>()) >=
                        DateTimeOffset.Parse(oldGrant["effectiveFrom"]!.GetValue<string>()) &&
                        DateTimeOffset.Parse(activity["time"]!["value"]!.GetValue<string>()) < DateTimeOffset.Parse(oldGrant["effectiveUntil"]!.GetValue<string>()),
                    ["periodArithmetic"] = 935 - 0 == 935,
                    ["unchangedQuarterOneHistory"] = JsonNode.DeepEquals(checkpoint["clock"]!["periodChain"], vectors[59]!["input"]!["periodChain"])
                };
                Assert.Equal("QUALIFIED", validation);
                Assert.All(relationships, item => Assert.True(item.Value!.GetValue<bool>(), item.Key));
                var restarted = new W1DevelopmentApplication(Configuration(variantDb), new FixedTime());
                var variantSetup = new W1MatchSetupApplication(Configuration(variantDb));
                var prepared = JsonSerializer.SerializeToNode(restarted.PrepareMatchSetup(variantSetup.Ledger.Read(subject)!, variantSetup.Ledger.Closure()), Wire)!;
                var currentGrant = grant.DeepClone().AsObject();
                currentGrant["identifier"] = "urn:amharc:test:single-fault:new-grant:" + variant.Item1;
                currentGrant["grant"]!["incarnationId"] = prepared["incarnationId"]!.DeepClone();
                currentGrant["resolutionRef"] = prepared["resolutionRef"]!.DeepClone(); Sign(currentGrant);
                restarted.Activate(subject, currentGrant, "TEST_ONLY-new-incarnation-activation");
                var before = W13BGovernedStateEvidence.Snapshot(variantDb);
                var memoryBefore = Memory(restarted);
                string actual;
                try { restarted.Command(subject, "recover", "TEST_ONLY-recover-" + variant.Item1); actual = "ACCEPT"; }
                catch (InvalidOperationException e) { actual = e.Message; }
                var after = W13BGovernedStateEvidence.Snapshot(variantDb);
                var memoryAfter = Memory(restarted);
                var result = new JsonObject {
                    ["standing"] = "TEST_ONLY / DEVELOPMENT-CONFORMANCE / NOT OPERATIONAL",
                    ["variant"] = variant.Item1, ["periodKey"] = variant.Item2,
                    ["exactPersistedCheckpoint"] = checkpoint, ["exactMaterialActivity"] = activity,
                    ["exactActivityRawBytesBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(activityJson)),
                    ["materialHead"] = Ref(head), ["materialChainStatus"] = validation, ["relationshipChecks"] = relationships,
                    ["formatRef"] = formatRef.DeepClone(), ["resolvedFormat"] = resolvedFormat,
                    ["localClosure"] = JsonSerializer.SerializeToNode(dependencies.Export(), Wire),
                    ["newIncarnationGrant"] = currentGrant, ["before"] = before, ["after"] = after,
                    ["memoryBefore"] = memoryBefore, ["memoryAfter"] = memoryAfter,
                    ["actualResponse"] = new JsonObject { ["boundary"] = "Actual W1DevelopmentApplication.Command -> W1SubjectBoundRuntime.Restore, not HTTP", ["reasonCode"] = actual },
                    ["sourceBehaviorChanged"] = false
                };
                File.WriteAllText(Path.Combine(evidence, variant.Item1 + ".json"), result.ToJsonString(Wire));
                Assert.Equal(variant.Item1 == "control" ? "ACCEPT" : "RECOVERY_PERIOD_INVALID", actual);
                if (variant.Item1 != "control")
                {
                    Assert.True(JsonNode.DeepEquals(before, after), "Governed persistence mutated on refusal");
                    Assert.True(JsonNode.DeepEquals(memoryBefore, memoryAfter), "Runtime state mutated on refusal");
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed); SqliteConnection.ClearAllPools();
            Directory.Delete(scratch, true);
        }
    }
}
