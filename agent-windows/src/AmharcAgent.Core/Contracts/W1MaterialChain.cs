using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Core.Contracts;

/// <summary>Bounded W1-owned chain, not an event-store platform. No timestamp head election.</summary>
public static class W1MaterialChain
{
    public static W1Reference Reference(W1JournalActivity a) => new(
        "urn:amharc:w1:activity:" + a.Id,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(a.Json))).ToLowerInvariant());

    public static string Validate(W1Reference head, string subject, JsonObject context,
        W1DependencyResolver dependencies, W1DevelopmentGrantVerifier verifier,
        JsonArray? membership = null)
    {
        var seen = new HashSet<string>();
        var current = head;
        for (var depth = 0; depth < 1024; depth++)
        {
            if (!seen.Add(current.Id)) throw new InvalidOperationException("W1_HISTORY_CYCLE");
            if (membership is not null && !membership.Any(p => W1DependencyResolver.Reference(p!) == current))
                throw new InvalidOperationException("W1_HISTORY_MEMBERSHIP_REFUSAL");
            var a = dependencies.Resolve(current); // exact raw-byte SHA, including every parent
            if (a["subject"]?.GetValue<string>() != subject ||
                !JsonNode.DeepEquals(a["exactContext"], context))
                throw new InvalidOperationException("W1_HISTORY_CONTEXT_REFUSAL");
            foreach (var r in context.Select(p => p.Value))
                _ = dependencies.Resolve(W1DependencyResolver.Reference(r!));
            if (a["baselineStanding"]?.GetValue<string>() == "legacy-assurance-gap")
                return "LEGACY_ASSURANCE_GAP";
            var grant = verifier.Verify((JsonObject)a["historicalGrantEvidence"]!, subject,
                a["incarnationId"]!.GetValue<string>()).Copy();
            var instant = DateTimeOffset.Parse(a["time"]!["value"]!.GetValue<string>());
            if (instant < DateTimeOffset.Parse(grant["effectiveFrom"]!.GetValue<string>()) ||
                instant >= DateTimeOffset.Parse(grant["effectiveUntil"]!.GetValue<string>()))
                throw new InvalidOperationException("W1_HISTORICAL_ELIGIBILITY_REFUSAL");
            if (a["parentRef"] is null)
            {
                if (a["baselineStanding"]?.GetValue<string>() != "governed-development-baseline" ||
                    a["inputActivity"] is not null)
                    throw new InvalidOperationException("W1_HISTORY_BASELINE_REFUSAL");
                return "QUALIFIED";
            }
            current = W1DependencyResolver.Reference(a["parentRef"]!);
            if (current.Id != "urn:amharc:w1:activity:" + a["inputActivity"]!.GetValue<string>())
                throw new InvalidOperationException("W1_HISTORY_PARENT_REFUSAL");
        }
        throw new InvalidOperationException("W1_HISTORY_DEPTH_REFUSAL");
    }

    public static W1Reference CurrentHead(IW1ClockJournal journal, W1JournalState checkpoint,
        W1DependencyResolver dependencies)
    {
        var all = journal.ReadActivities(checkpoint.Subject);
        if (all.Count == 0) throw new InvalidOperationException("W1_HISTORY_UNRESOLVED");
        var parents = new HashSet<string>();
        foreach (var a in all)
        {
            var value = JsonNode.Parse(a.Json)!;
            if (value["subject"]?.GetValue<string>() != checkpoint.Subject || a.Subject != checkpoint.Subject ||
                value["activityId"]?.GetValue<string>() != a.Id ||
                value["identifier"]?.GetValue<string>() != "urn:amharc:w1:activity:" + a.Id)
                throw new InvalidOperationException("W1_HISTORY_SUBJECT_REFUSAL");
            if (value["inputActivity"] is { } p) parents.Add(p.GetValue<string>());
            dependencies.Add(Reference(a), Encoding.UTF8.GetBytes(a.Json));
        }
        var heads = all.Where(a => !parents.Contains(a.Id)).ToArray();
        if (heads.Length != 1) throw new InvalidOperationException("W1_HISTORY_COMPETING_HEADS");
        if (heads[0].Id != checkpoint.ActivityId)
            throw new InvalidOperationException("W1_STALE_CHECKPOINT_REFUSAL");
        return Reference(heads[0]);
    }
}