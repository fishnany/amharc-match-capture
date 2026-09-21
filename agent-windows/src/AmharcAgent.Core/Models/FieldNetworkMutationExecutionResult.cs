namespace AmharcAgent.Core.Models;

public enum FieldNetworkMutationExecutionStatus
{
    NoAction,
    Applied,
    Refused,
    Failed
}

public sealed record FieldNetworkMutationActionResult(
    FieldNetworkMutationCommandKind Kind,
    bool Succeeded,
    string Message);

public sealed record FieldNetworkMutationExecutionResult(
    DateTimeOffset ExecutedAt,
    FieldNetworkMutationExecutionStatus Status,
    string Reason,
    IReadOnlyList<FieldNetworkMutationActionResult> Actions);