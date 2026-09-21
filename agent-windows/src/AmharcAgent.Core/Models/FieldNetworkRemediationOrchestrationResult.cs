namespace AmharcAgent.Core.Models;

public enum FieldNetworkRemediationOrchestrationStatus
{
    NoAction,
    Refused,
    Applied,
    Failed
}

/// <summary>
/// Auditable outcome of one explicit field-network remediation attempt.
/// </summary>
public sealed record FieldNetworkRemediationOrchestrationResult(
    DateTimeOffset CompletedAt,
    FieldNetworkRemediationOrchestrationStatus Status,
    string Reason,
    FieldNetworkState InitialState,
    FieldNetworkRemediationPlan Plan,
    FieldNetworkRemediationExecutionResult? DryRun,
    IReadOnlyList<FieldNetworkMutationCommand> Commands,
    FieldNetworkMutationExecutionResult? Mutation,
    FieldNetworkState FinalState);