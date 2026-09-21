namespace AmharcAgent.Core.Models;

public enum FieldNetworkRemediationDisposition
{
    NoAction,
    RepairAvailable,
    Refused
}

public enum FieldNetworkRepairActionKind
{
    SetCaptureIpv4,
    RemoveCaptureGateway,
    EnsureCaptureSubnetRoute,
    RemoveCaptureDefaultRoute
}

public sealed record FieldNetworkRepairAction(
    FieldNetworkRepairActionKind Kind,
    string Summary);

/// <summary>
/// A deterministic, non-executing repair plan. This contract describes what
/// AMHARC is permitted to repair; it does not perform operating-system changes.
/// </summary>
public sealed record FieldNetworkRemediationPlan(
    DateTimeOffset PlannedAt,
    FieldNetworkRemediationDisposition Disposition,
    string Reason,
    string? CaptureAdapterName,
    IReadOnlyList<FieldNetworkRepairAction> Actions)
{
    public bool RequiresChange =>
        Disposition == FieldNetworkRemediationDisposition.RepairAvailable &&
        Actions.Count > 0;
}