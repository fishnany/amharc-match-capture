using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;

namespace AmharcAgent.Api.W1;

/// <summary>
/// Two TEST_ONLY persisted recovery premises. This builder never invokes recovery
/// or chooses its verdict. It only appends an immutable material activity and
/// matching checkpoint via the existing atomic journal transaction.
/// </summary>
internal static class W1InstalledAssuranceFixture
{
    internal const string UnresolvedFormat = "recovery-unresolved-format";
    internal const string InvalidPeriod = "recovery-current-period-outside-format";

    internal static object Prepare(string subject, string scenario, string operationKey,
        W1SqliteClockJournal journal, W1DependencyResolver dependencies,
        W1DevelopmentTrustAnchor anchor, TimeProvider time, JsonObject setup)
    {
        if (scenario is not (UnresolvedFormat or InvalidPeriod))
            throw new InvalidOperationException("W1_ASSURANCE_SCENARIO_REFUSAL");
        if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 160)
            throw new InvalidOperationException("W1_ASSURANCE_OPERATION_KEY_REFUSAL");
        var input = new JsonObject { ["subject"] = subject, ["scenario"] = scenario };
        var inputHash = W1CanonicalJson.Digest(input.ToJsonString());

        object? Prior()
        {
            var previous = journal.FindOperation(subject, operationKey);
            if (previous is null) return null;
            var recorded = JsonNode.Parse(previous.Json)?["installedAssuranceFixture"];
            if (previous.InputSha256 != inputHash || recorded?["scenario"]?.GetValue<string>() != scenario)
                throw new InvalidOperationException("W1_ASSURANCE_FIXTURE_CONFLICT");
            return Receipt(previous);
        }
        if (Prior() is { } historical) return historical;
        // One scenario per synthetic subject. Do not prepare a second fault in
        // the same material chain, even under a different logical operation key.
        if (journal.ReadActivities(subject).Any(a => JsonNode.Parse(a.Json)?["installedAssuranceFixture"] is not null))
            throw new InvalidOperationException("W1_ASSURANCE_FIXTURE_ALREADY_PREPARED");
        var original = journal.Read(subject) ?? throw new InvalidOperationException("W1_ASSURANCE_CHECKPOINT_UNRESOLVED");
        var checkpoint = JsonNode.Parse(original.ContextJson)!.AsObject();
        if (checkpoint["matchSetup"] is not JsonObject persistedSetup ||
            !JsonNode.DeepEquals(persistedSetup, setup) ||
            checkpoint["subject"]?["occurrenceId"]?.GetValue<string>() != subject ||
            checkpoint["status"]?.GetValue<string>() != "active")
            throw new InvalidOperationException("W1_ASSURANCE_SYNTHETIC_CONTEXT_REFUSAL");
        var current = (JsonObject)checkpoint["clock"]!.DeepClone();
        var formatRef = W1DependencyResolver.Reference(setup["formatRef"]!);
        var format = dependencies.Resolve(formatRef);
        var admission = new W1MatchSetupAdmission(dependencies);
        var validCheckpoint = (JsonObject)setup.DeepClone();
        validCheckpoint["subject"] = subject;
        validCheckpoint["historyFormatRef"] = setup["formatRef"]!.DeepClone();
        validCheckpoint["historyAssignmentRef"] = setup["assignmentRef"]!.DeepClone();
        validCheckpoint["currentPeriodKey"] = current["periodKey"]!.DeepClone();
        validCheckpoint["periodChain"] = current["periodChain"]!.DeepClone();
        validCheckpoint["accumulatedSeconds"] = current["accumulated"]!.DeepClone();
        validCheckpoint["periodStartSeconds"] = current["periodStart"]!.DeepClone();
        validCheckpoint["periodElapsedSeconds"] = current["periodElapsed"]!.DeepClone();
        var bound = (JsonObject)setup.DeepClone();
        bound["subject"] = subject;
        admission.AdmitRecovery(validCheckpoint, bound, true);

        var head = W1MaterialChain.CurrentHead(journal, original, dependencies);
        if (W1DependencyResolver.Reference(checkpoint["activityRef"]!) != head ||
            W1MaterialChain.Validate(head, subject, (JsonObject)checkpoint["context"]!,
                dependencies, new W1DevelopmentGrantVerifier(anchor)) != "QUALIFIED")
            throw new InvalidOperationException("W1_ASSURANCE_HISTORY_REFUSAL");
        var parent = journal.ReadActivity(original.ActivityId)
            ?? throw new InvalidOperationException("W1_ASSURANCE_HISTORY_REFUSAL");
        var oldActivity = JsonNode.Parse(parent.Json)!.AsObject();
        if (!JsonNode.DeepEquals(oldActivity["newState"], checkpoint["clock"]) ||
            !JsonNode.DeepEquals(oldActivity["historicalGrantEvidence"], checkpoint["historicalGrantEvidence"]))
            throw new InvalidOperationException("W1_ASSURANCE_HISTORY_REFUSAL");
        var now = time.GetUtcNow();
        var oldGrant = new W1DevelopmentGrantVerifier(anchor).Verify(
            (JsonObject)checkpoint["historicalGrantEvidence"]!, subject,
            checkpoint["incarnationId"]!.GetValue<string>()).Copy();
        if (now < DateTimeOffset.Parse(oldGrant["effectiveFrom"]!.GetValue<string>()) ||
            now >= DateTimeOffset.Parse(oldGrant["effectiveUntil"]!.GetValue<string>()))
            throw new InvalidOperationException("W1_ASSURANCE_HISTORICAL_ELIGIBILITY_REFUSAL");

        var next = (JsonObject)current.DeepClone();
        if (scenario == InvalidPeriod)
        {
            if (((JsonArray)format["periods"]!).Any(p => p?["key"]?.GetValue<string>() == "extra-time-half-1"))
                throw new InvalidOperationException("W1_ASSURANCE_SCENARIO_REFUSAL");
            next["periodKey"] = "extra-time-half-1";
        }
        else
        {
            // Content-address an intentionally absent TEST_ONLY document; do not
            // remove or replace any installed controlled member/current setup.
            var missing = new JsonObject {
                ["identifier"] = "urn:amharc:test:w1-3c:unresolved-format:" +
                    W1CanonicalJson.Digest(input.ToJsonString()),
                ["version"] = "1.0.0", ["standing"] = "TEST_ONLY / NOT INSTALLED"
            };
            var bytes = Encoding.UTF8.GetBytes(missing.ToJsonString());
            checkpoint["matchSetup"]!["formatRef"] = new JsonObject {
                ["id"] = missing["identifier"]!.DeepClone(), ["version"] = "1.0.0",
                ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
            };
        }
        var beforeFingerprint = W1CanonicalJson.Digest(original.ContextJson);
        var id = Guid.NewGuid().ToString("D");
        var activity = (JsonObject)oldActivity.DeepClone();
        activity["identifier"] = "urn:amharc:w1:activity:" + id;
        activity["activityId"] = id;
        activity["kind"] = "installed-assurance-fixture";
        activity["logicalOperationKey"] = operationKey;
        activity["inputRevision"] = original.Revision.ToString();
        activity["inputActivity"] = original.ActivityId;
        activity["parentRef"] = W1DevelopmentApplication.Ref(head);
        activity["priorState"] = current.DeepClone();
        activity["newState"] = next.DeepClone();
        activity["priorSeconds"] = current["accumulated"]!.DeepClone();
        activity["newSeconds"] = next["accumulated"]!.DeepClone();
        activity["sourceBasis"] = "TEST_ONLY installed recovery assurance premise; no recovery verdict";
        activity["time"] = new JsonObject { ["domain"] = "UTC", ["value"] = now.ToString("O") };
        activity["installedAssuranceFixture"] = new JsonObject {
            ["scenario"] = scenario, ["beforeStateFingerprint"] = beforeFingerprint,
            ["formatRef"] = checkpoint["matchSetup"]!["formatRef"]!.DeepClone(),
            ["standing"] = "TEST_ONLY / DEVELOPMENT-CONFORMANCE / NOT OPERATIONAL"
        };
        checkpoint["clock"] = next.DeepClone();
        checkpoint["activityId"] = id;
        var material = new W1JournalActivity(id, subject, operationKey, inputHash, activity.ToJsonString());
        checkpoint["activityRef"] = W1DevelopmentApplication.Ref(W1MaterialChain.Reference(material));
        try
        {
            journal.Commit(original.Revision, new(subject, original.Revision + 1, "1.0.0",
                checkpoint.ToJsonString(), id), material);
        }
        catch (InvalidOperationException e) when (e.Message == "W1_CHECKPOINT_CONFLICT")
        {
            // A concurrent process may have committed exactly this operation;
            // never run a second append for a retry.
            var duplicateReceipt = Prior();
            if (duplicateReceipt is not null) return duplicateReceipt;
            throw;
        }
        return Receipt(material);
    }

    private static object Receipt(W1JournalActivity activity)
    {
        var value = JsonNode.Parse(activity.Json)!;
        var fixture = value["installedAssuranceFixture"]!;
        return new {
            standing = "TEST_ONLY / DEVELOPMENT-CONFORMANCE / NOT OPERATIONAL",
            scenario = fixture["scenario"]!.GetValue<string>(),
            subject = activity.Subject,
            fixtureOperationKey = activity.OperationKey,
            checkpointReference = new { subject = activity.Subject,
                revision = long.Parse(value["inputRevision"]!.GetValue<string>()) + 1,
                activityId = activity.Id },
            materialHead = W1DevelopmentApplication.Ref(W1MaterialChain.Reference(activity)),
            formatReference = fixture["formatRef"]!.DeepClone(),
            activityReference = W1DevelopmentApplication.Ref(W1MaterialChain.Reference(activity)),
            beforeStateFingerprint = fixture["beforeStateFingerprint"]!.GetValue<string>(),
            preparedStateFingerprint = W1MaterialChain.Reference(activity).Sha256
        };
    }
}
