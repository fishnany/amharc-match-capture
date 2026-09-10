namespace AmharcAgent.Core.Models;

public enum FieldNetworkRemediationExecutionStatus
{
    NoAction,
    DryRunVerified,
    Refused
}

public sealed record FieldNetworkRemediationExecutionResult(
    DateTimeOffset ExecutedAt,
    FieldNetworkRemediationExecutionStatus Status,
    string Reason,
    string? CaptureAdapterName,
    IReadOnlyList<FieldNetworkRepairAction> ValidatedActions);