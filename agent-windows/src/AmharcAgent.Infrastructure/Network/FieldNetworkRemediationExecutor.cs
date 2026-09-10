using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Safety boundary for future Windows field-network remediation.
/// Phase 4C2 performs validation and dry-run verification only.
/// It contains no operating-system mutation capability.
/// </summary>
internal sealed class FieldNetworkRemediationExecutor
{
    public FieldNetworkRemediationExecutionResult ExecuteDryRun(
        FieldNetworkProfile profile,
        WindowsFieldNetworkObservation freshObservation,
        FieldNetworkRemediationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(freshObservation);
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Disposition == FieldNetworkRemediationDisposition.NoAction)
        {
            return Result(
                freshObservation.ObservedAt,
                FieldNetworkRemediationExecutionStatus.NoAction,
                "No remediation is required.",
                plan.CaptureAdapterName,
                Array.Empty<FieldNetworkRepairAction>());
        }

        if (plan.Disposition != FieldNetworkRemediationDisposition.RepairAvailable ||
            plan.Actions.Count == 0)
        {
            return Refused(
                freshObservation.ObservedAt,
                "Only a non-empty RepairAvailable plan may enter the remediation executor.");
        }

        var candidates = freshObservation.Adapters
            .Where(a => a.IsUp)
            .Where(a =>
                EqualsIgnoreCase(a.Alias, profile.CaptureAdapterAlias) &&
                EqualsIgnoreCase(NormaliseMac(a.MacAddress), NormaliseMac(profile.CaptureAdapterMacAddress)))
            .ToArray();

        if (candidates.Length != 1)
        {
            return Refused(
                freshObservation.ObservedAt,
                "Fresh hardware identity validation did not resolve exactly one canonical Capture adapter.");
        }

        var adapter = candidates[0];

        if (!EqualsIgnoreCase(plan.CaptureAdapterName, profile.CaptureAdapterAlias) ||
            !EqualsIgnoreCase(adapter.Alias, plan.CaptureAdapterName))
        {
            return Refused(
                freshObservation.ObservedAt,
                "The repair plan is not bound to the freshly validated canonical Capture adapter.");
        }

        var externalDefaultPresent = freshObservation.Routes.Any(r =>
            r.InterfaceIndex != adapter.InterfaceIndex &&
            EqualsIgnoreCase(r.DestinationPrefix, "0.0.0.0/0"));

        if (!externalDefaultPresent)
        {
            return Refused(
                freshObservation.ObservedAt,
                "No external default route exists. The executor will not risk isolating the host.");
        }

        var freshPlan = FieldNetworkRemediationPlanner.Plan(profile, freshObservation);

        if (freshPlan.Disposition == FieldNetworkRemediationDisposition.Refused)
        {
            return Refused(
                freshObservation.ObservedAt,
                $"Fresh observation refused remediation: {freshPlan.Reason}");
        }

        var requested = plan.Actions.Select(a => a.Kind).OrderBy(x => x).ToArray();
        var currentlyRequired = freshPlan.Actions.Select(a => a.Kind).OrderBy(x => x).ToArray();

        if (!requested.SequenceEqual(currentlyRequired))
        {
            return Refused(
                freshObservation.ObservedAt,
                "The requested repair plan is stale or does not exactly match the fresh machine state.");
        }

        foreach (var action in plan.Actions)
        {
            if (!Enum.IsDefined(typeof(FieldNetworkRepairActionKind), action.Kind))
            {
                return Refused(
                    freshObservation.ObservedAt,
                    "The repair plan contains an unknown action kind.");
            }

            if (!IsCanonicalAction(action.Kind))
            {
                return Refused(
                    freshObservation.ObservedAt,
                    $"Action {action.Kind} is outside the bounded AMHARC remediation authority.");
            }
        }

        return Result(
            freshObservation.ObservedAt,
            FieldNetworkRemediationExecutionStatus.DryRunVerified,
            "Fresh hardware identity and bounded repair actions verified. No Windows changes were executed.",
            adapter.Alias,
            plan.Actions.ToArray());
    }

    private static bool IsCanonicalAction(FieldNetworkRepairActionKind kind) =>
        kind is
            FieldNetworkRepairActionKind.SetCaptureIpv4 or
            FieldNetworkRepairActionKind.RemoveCaptureGateway or
            FieldNetworkRepairActionKind.EnsureCaptureSubnetRoute or
            FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute;

    private static FieldNetworkRemediationExecutionResult Refused(
        DateTimeOffset at,
        string reason) =>
        Result(
            at,
            FieldNetworkRemediationExecutionStatus.Refused,
            reason,
            null,
            Array.Empty<FieldNetworkRepairAction>());

    private static FieldNetworkRemediationExecutionResult Result(
        DateTimeOffset at,
        FieldNetworkRemediationExecutionStatus status,
        string reason,
        string? adapter,
        IReadOnlyList<FieldNetworkRepairAction> actions) =>
        new(at, status, reason, adapter, actions);

    private static bool EqualsIgnoreCase(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string NormaliseMac(string? value) =>
        (value ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
}