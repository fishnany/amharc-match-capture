using System.Text.Json.Nodes;

namespace AmharcAgent.Core.Contracts;

/// <summary>Successor policy for the new application path. It returns an admission
/// decision only; the existing runtime owns attributable material mutation.</summary>
public sealed class W1OrderedMatchFormat(JsonObject exactFormat)
{
    private readonly JsonObject _format = (JsonObject)exactFormat.DeepClone();
    private static string Text(JsonNode? n) => W1MatchSetupAdmission.Text(n);
    public string InitialPeriod => Text(_format["periods"]![0]!["key"]);
    public JsonObject Definition => (JsonObject)_format.DeepClone();
    public void Admit(string currentPeriod, string operation, string? nextPeriod,
        bool completed, bool periodTransitionEligible, string? competentExtraTimeDecision)
    {
        if (completed) throw new InvalidOperationException("ALREADY_COMPLETED");
        if (!periodTransitionEligible) throw new InvalidOperationException("PERIOD_TRANSITION_INELIGIBLE");
        var periods = ((JsonArray)_format["periods"]!).Select(p => (JsonObject)p!).ToArray();
        var current = periods.SingleOrDefault(p => Text(p["key"]) == currentPeriod)
            ?? throw new InvalidOperationException("RECOVERY_PERIOD_INVALID");
        var next = periods.SingleOrDefault(p => Text(p["key"]) == nextPeriod);
        if (next is not null && Text(next["sportingPhase"]) == "EXTRA_TIME" &&
            Text(_format["extraTime"]?["applicability"]) == "NOT_APPLICABLE")
            throw new InvalidOperationException("ET_NOT_APPLICABLE");
        if (nextPeriod?.StartsWith("extra-time", StringComparison.Ordinal) == true &&
            Text(_format["extraTime"]?["applicability"]) == "NOT_APPLICABLE")
            throw new InvalidOperationException("ET_NOT_APPLICABLE");
        var successors = ((JsonArray)current["successors"]!).Select(s => (JsonObject)s!).ToArray();
        var candidate = successors.SingleOrDefault(s => Text(s["operation"]) == operation &&
            (s["nextPeriodKey"] is null ? nextPeriod is null : Text(s["nextPeriodKey"]) == nextPeriod));
        if (candidate is null)
            throw new InvalidOperationException(operation == "COMPLETE" ? "WRONG_COMPLETION" : "WRONG_SUCCESSOR");
        var condition = Text(candidate["condition"]);
        if (condition != "ALWAYS" && competentExtraTimeDecision != condition)
            throw new InvalidOperationException("ET_DECISION_UNPROVEN");
    }
    public void AdmitRecovery(JsonObject checkpoint, JsonObject boundContext, bool newIncarnationEligible)
    {
        foreach (var key in new[] { "subject", "formatRef", "assignmentRef", "dependencyRefs", "bindingRef", "principalRef" })
            if (!JsonNode.DeepEquals(checkpoint[key], boundContext[key]))
                throw new InvalidOperationException("CHECKPOINT_CONTEXT_MISMATCH");
        foreach (var key in new[] { "historyFormatRef", "historyAssignmentRef" })
        {
            var binding = key == "historyFormatRef" ? "formatRef" : "assignmentRef";
            if (!JsonNode.DeepEquals(checkpoint[key], boundContext[binding]))
                throw new InvalidOperationException("CHECKPOINT_CONTEXT_MISMATCH");
        }
        var periods = ((JsonArray)_format["periods"]!).Select(p => (JsonObject)p!).ToArray();
        var current = Text(checkpoint["currentPeriodKey"]);
        if (!periods.Any(p => Text(p["key"]) == current))
            throw new InvalidOperationException("RECOVERY_PERIOD_INVALID");
        var chain = checkpoint["periodChain"] as JsonArray ?? throw new InvalidOperationException("RECOVERY_CHAIN_INVALID");
        if (chain.Count == 0 || Text(chain[0]?["periodKey"]) != InitialPeriod ||
            Text(chain[^1]?["periodKey"]) != current) throw new InvalidOperationException("RECOVERY_CHAIN_INVALID");
        for (var i = 1; i < chain.Count; i++)
        {
            var prior = periods.Single(p => Text(p["key"]) == Text(chain[i-1]?["periodKey"]));
            if (!((JsonArray)prior["successors"]!).Any(s => Text(s?["operation"]) == "NEXT_PERIOD" &&
                Text(s?["nextPeriodKey"]) == Text(chain[i]?["periodKey"])))
                throw new InvalidOperationException("RECOVERY_CHAIN_INVALID");
            if (chain[i]!["periodStartSeconds"]!.GetValue<int>() < chain[i-1]!["periodStartSeconds"]!.GetValue<int>())
                throw new InvalidOperationException("RECOVERY_CHAIN_INVALID");
        }
        if (checkpoint["accumulatedSeconds"]!.GetValue<int>() - checkpoint["periodStartSeconds"]!.GetValue<int>()
            != checkpoint["periodElapsedSeconds"]!.GetValue<int>())
            throw new InvalidOperationException("RECOVERY_CHAIN_INVALID");
        if (checkpoint["requestedNewIncarnation"] is not null && !newIncarnationEligible)
            throw new InvalidOperationException("RECOVERY_INCARCATION_INELIGIBLE");
    }
}