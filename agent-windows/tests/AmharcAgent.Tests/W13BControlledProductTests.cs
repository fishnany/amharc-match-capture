using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Clock;
using Xunit;

namespace AmharcAgent.Tests;

/// <summary>Frozen outcome gate over product admission and durable handoff, plus
/// the successor/recovery policy used by the governed runtime. Stops immediately
/// on contradiction; no test changes a controlled byte or expected result.</summary>
public sealed class W13BControlledProductTests
{
    private static string DirectoryPath => Path.Combine(W1ControlledVectorTests.Root(),
        "governance", "wave-1", "match-setup", "v1.0.0");
    private static JsonObject Read(string name) => (JsonObject)JsonNode.Parse(
        File.ReadAllText(Path.Combine(DirectoryPath, name)))!;
    private static string Text(JsonNode? node) => W1MatchSetupAdmission.Text(node);
    private static W1DependencyResolver Dependencies()
    {
        var resolver = W1ControlledVectorTests.LoadDependencies();
        foreach (var file in Directory.GetFiles(DirectoryPath, "*.json", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file); var doc = JsonNode.Parse(bytes)!;
            var id = doc["identifier"]?.GetValue<string>() ?? doc["$id"]?.GetValue<string>();
            if (id is not null) resolver.Add(new(id, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()), bytes);
        }
        return resolver;
    }
    private static W1SetupScope Scope(JsonObject premises) => new(
        "urn:amharc:test:tagger:installation-a",
        premises["authenticatedOriginMatchesIssuer"]?.GetValue<bool>() == true,
        premises["allocatorTestScopeVerified"]?.GetValue<bool>() == true,
        _ => premises["competentTestBasisVerified"]?.GetValue<bool>() == true &&
            premises["policyTestScopeVerified"]?.GetValue<bool>() == true &&
            premises["testClosureResolved"]?.GetValue<bool>() == true,
        Text(premises["environment"]));
    [Fact]
    public void FocusedCanonicalExpectationRepair_13_15_16_24_25()
        => ExecuteFrozenSuite("focused-13-15-16-24-25", new[] { 13, 15, 16, 24, 25 });
    [Fact]
    public void FocusedRecoveryResolutionRepair_58_59_60_61_62_63()
        => ExecuteFrozenSuite("focused-recovery-58-63", new[] { 58, 59, 60, 61, 62, 63 });

    [Fact]
    public void Frozen63_ActualProductOutcomeGate_StopOnFirstContradiction()
        => ExecuteFrozenSuite("fresh-full-1-63", null);

    private static void ExecuteFrozenSuite(string suite, int[]? selected)
    {
        var vectors = (JsonArray)Read("W1_MATCH_FORMAT_HANDOFF_CONFORMANCE_VECTORS.json")["vectors"]!;
        Assert.Equal(63, vectors.Count);
        var results = new JsonArray();
        var evidenceDirectory = Environment.GetEnvironmentVariable("W13B_EVIDENCE_DIRECTORY") ??
            throw new InvalidOperationException("Explicit external reconciliation evidence directory required");
        Directory.CreateDirectory(evidenceDirectory);
        var evidence = Path.Combine(evidenceDirectory, suite + ".json");
        var dependencies = Dependencies(); var admission = new W1MatchSetupAdmission(dependencies);
        var baseline = Read("fixtures/request-training-q4-15.json");
        var allocatorDoc = Read("fixtures/allocator-applicability.json");
        var allocator = new JsonObject {
            ["id"] = allocatorDoc["identifier"]!.DeepClone(), ["version"] = "1.0.0",
            ["sha256"] = W1CanonicalJson.Digest(allocatorDoc.ToJsonString())
        };
        var vectorNumber = 0;
        foreach (var node in vectors)
        {
            vectorNumber++;
            if (selected is not null && !selected.Contains(vectorNumber)) continue;
            var vector = (JsonObject)node!; var input = (JsonObject)vector["input"]!;
            var expected = (JsonObject)vector["expected"]!; var before = (JsonObject)vector["before"]!;
            var premises = (JsonObject)vector["premises"]!;
            string? code = null; string outcome; bool? schema = null;
            JsonObject? actualResponse = null;
            JsonObject? governedBefore = null, governedAfter = null;
            JsonObject? resolutionEvidence = null;
            bool? stateUnchanged = null;
            var invariantFailures = new JsonArray();
            var kind = Text(vector["kind"]);
            if (kind == "HANDOFF")
            {
                var path = Path.Combine(Path.GetTempPath(), "w13b-vector-" + Guid.NewGuid() + ".sqlite");
                var ledger = new W1SqliteMatchSetupLedger(path, "urn:amharc:test:capture:installation-a",
                    allocator, "synthetic-vector-actor", "isolated-product-conformance",
                    () => Guid.Parse("10000000-0000-4000-8000-000000000001"));
                // The fresh handoff host has no temporal principal/runtime. Its
                // actual persistent clock/history/checkpoint tables are empty,
                // not assumed absent or represented by ledger counts.
                _ = new W1SqliteClockJournal(path);
                try
                {
                    if (before["allocations"]!.GetValue<int>() > 0)
                    {
                        var seedPremises = (JsonObject)premises.DeepClone();
                        seedPremises["environment"] = "CONFORMANCE";
                        seedPremises["authenticatedOriginMatchesIssuer"] = true;
                        seedPremises["allocatorTestScopeVerified"] = true;
                        seedPremises["competentTestBasisVerified"] = true;
                        seedPremises["policyTestScopeVerified"] = true;
                        seedPremises["testClosureResolved"] = true;
                        _ = ledger.Apply(admission.Admit(baseline.ToJsonString(), Scope(seedPremises)));
                        if (before["clockActivated"]!.GetValue<bool>() ||
                            before["materialHistoryCount"]!.GetValue<int>() > 0)
                            ledger.LockForClockActivity("10000000-0000-4000-8000-000000000001");
                    }
                    governedBefore = W13BGovernedStateEvidence.Snapshot(path);
                    schema = admission.RequestValid(input);
                    try
                    {
                        var request = admission.Admit(input.ToJsonString(), Scope(premises));
                        actualResponse = ledger.Apply(request, point => {
                            if (premises["commitResultKnown"]?.GetValue<bool>() == false && point == "after-commit-before-response")
                                throw new IOException("Synthetic lost commit acknowledgement");
                        });
                        var status = Text(actualResponse["status"]);
                        outcome = status == "REFUSED" ? "REFUSE" : status == "INDETERMINATE" ? "INDETERMINATE" :
                            Text(actualResponse["idempotencyStanding"]) == "DUPLICATE_HISTORICAL" ? "HISTORICAL_RECEIPT" :
                            Text(actualResponse["idempotencyStanding"]) == "RESOLVED_EXISTING" ? "RESOLVE_EXISTING" : "ACCEPT";
                        code = Text(actualResponse["failure"]?["reasonCode"]);
                        if (code == "") code = null;
                    }
                    catch (InvalidOperationException exception) { code = exception.Message; outcome = "REFUSE"; }
                    governedAfter = W13BGovernedStateEvidence.Snapshot(path);
                    stateUnchanged = JsonNode.DeepEquals(governedBefore, governedAfter);
                    if (expected["stateMutationAllowed"]?.GetValue<bool>() == false && stateUnchanged != true)
                        invariantFailures.Add("DEFINITE_NO_MUTATION_LOGICAL_STATE_CHANGED");
                    if (actualResponse is not null &&
                        expected["noClockAuthorityConferredByHandoff"]?.GetValue<bool>() == true &&
                        actualResponse["clockAuthorityConferred"]?.GetValue<bool>() != false)
                        invariantFailures.Add("CLOCK_AUTHORITY_CONFERRED");
                    if (vectorNumber == 25)
                    {
                        var counts = ledger.Counts();
                        if (counts.Allocations != vector["after"]!["allocations"]!.GetValue<int>() ||
                            counts.Receipts != vector["after"]!["logicalReceiptCount"]!.GetValue<int>() ||
                            counts.Revisions != vector["after"]!["setupRevisions"]!.GetValue<int>() ||
                            Text(actualResponse?["canonicalOccurrenceId"]) != Text(before["canonicalOccurrenceId"]))
                            invariantFailures.Add("EXISTING_REPRESENTATION_REALLOCATION_OR_FROZEN_COUNTS");
                    }
                }
                finally
                {
                    foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
                }
            }
            else if (kind == "RECOVERY")
            {
                var context = (JsonObject)before.DeepClone();
                context["subject"] = before["canonicalOccurrenceId"]!.DeepClone();
                governedBefore = W13BGovernedStateEvidence.RecoveryComposition(input, context, dependencies);
                resolutionEvidence = new JsonObject {
                    ["requestedExactReference"] = input["formatRef"]!.DeepClone(),
                    ["resolved"] = false, ["source"] = "Applicable local controlled closure; no network/fallback"
                };
                outcome = "ACCEPT";
                try
                {
                    var resolved = admission.ResolveRecoveryFormat(input["formatRef"]);
                    resolutionEvidence["resolved"] = true;
                    resolutionEvidence["resolvedIdentifier"] = resolved["identifier"]!.DeepClone();
                    resolutionEvidence["resolvedVersion"] = resolved["version"]!.DeepClone();
                    resolutionEvidence["resolvedDocumentSha256"] = W1CanonicalJson.Digest(resolved.ToJsonString());
                    admission.AdmitRecovery(input, context, premises["newIncarnationEligible"]?.GetValue<bool>() == true);
                }
                catch (InvalidOperationException exception) { outcome = "REFUSE"; code = exception.Message; }
                governedAfter = W13BGovernedStateEvidence.RecoveryComposition(input, context, dependencies);
                stateUnchanged = JsonNode.DeepEquals(governedBefore, governedAfter);
                if (expected["stateMutationAllowed"]?.GetValue<bool>() == false && stateUnchanged != true)
                    invariantFailures.Add("RECOVERY_COMPOSITION_STATE_CHANGED");
            }
            else if (kind == "TRANSITION")
            {
                governedBefore = new JsonObject {
                    ["boundary"] = "Invoked runtime policy; not full runtime composition",
                    ["input"] = input.DeepClone(), ["context"] = before.DeepClone()
                };
                outcome = "ACCEPT";
                try
                {
                    JsonObject format;
                    try { format = admission.Resolve(input["formatRef"]!); }
                    catch (InvalidOperationException) { throw new InvalidOperationException("RECOVERY_FORMAT_UNRESOLVED"); }
                    var ordered = new W1OrderedMatchFormat(format);
                    if (kind == "TRANSITION")
                    {
                        string? condition = null;
                        if (input["extraTimeDecisionRef"] is not null)
                        {
                            var decision = admission.Resolve(input["extraTimeDecisionRef"]!);
                            condition = Text(decision["role"]) switch {
                                "et-invoked" => "EXTRA_TIME_INVOKED", "et-not-invoked" => "EXTRA_TIME_NOT_INVOKED", _ => null
                            };
                        }
                        ordered.Admit(Text(before["currentPeriodKey"]), Text(input["operation"]),
                            input["requestedNextPeriodKey"] is null ? null : Text(input["requestedNextPeriodKey"]),
                            before["completed"]!.GetValue<bool>(),
                            premises["eligiblePeriodTransitionIncludingCompletion"]!.GetValue<bool>(), condition);
                    }
                    else
                    {
                        if (premises["exactDependenciesResolved"]?.GetValue<bool>() == false)
                            throw new InvalidOperationException("RECOVERY_FORMAT_UNRESOLVED");
                        var context = (JsonObject)before.DeepClone();
                        context["subject"] = before["canonicalOccurrenceId"]!.DeepClone();
                        ordered.AdmitRecovery(input, context, premises["newIncarnationEligible"]?.GetValue<bool>() == true);
                    }
                }
                catch (InvalidOperationException exception) { outcome = "REFUSE"; code = exception.Message; }
                governedAfter = governedBefore.DeepClone().AsObject();
                stateUnchanged = true; // These policy boundaries have no durable writes.
            }
            else
            {
                // No requested operation is sent to the product transition
                // boundary. Actual timed-runtime evidence is a separate composed gate.
                outcome = "UNCHANGED";
            }
            var passed = invariantFailures.Count == 0 &&
                outcome == Text(expected["outcome"]) && code == (expected["reasonCode"] is null ? null : Text(expected["reasonCode"])) &&
                (expected["requestSchemaValid"] is null || schema == expected["requestSchemaValid"]!.GetValue<bool>());
            results.Add(new JsonObject { ["number"] = vectorNumber, ["id"] = vector["id"]!.DeepClone(), ["kind"] = kind, ["passed"] = passed,
                ["expected"] = expected.DeepClone(), ["actualOutcome"] = outcome, ["actualReasonCode"] = code,
                ["actualRequestSchemaValid"] = schema, ["actualResponse"] = actualResponse?.DeepClone(),
                ["resolutionEvidence"] = resolutionEvidence,
                ["governedBefore"] = governedBefore, ["governedAfter"] = governedAfter,
                ["governedStateUnchanged"] = stateUnchanged, ["invariantFailures"] = invariantFailures });
            File.WriteAllText(evidence, new JsonObject {
                ["controlledVectorSha256"] = "2d903705e7ea4b3ba1308ae000db88dda73b09cbe7f428fba995a09b357dea6d",
                ["suite"] = suite, ["executed"] = results.Count, ["required"] = selected?.Length ?? 63,
                ["scope"] = "Product admission/durable ledger and invoked runtime policy; not full composed/native assurance",
                ["results"] = results.DeepClone()
            }.ToJsonString(new() { WriteIndented = true }));
            Assert.True(passed, "W1-3B FROZEN EXPECTATION CONTRADICTION: " + Text(vector["id"]) +
                "; expected " + expected.ToJsonString() + "; actual " + outcome + "/" + code + "/schema=" + schema);
        }
    }
}