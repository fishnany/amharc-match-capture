using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Production coordinator for one explicit bounded remediation attempt.
/// It never bypasses the mutation authority/provider safety gates.
/// </summary>
internal sealed class FieldNetworkRemediationOrchestrator(
    IWindowsFieldNetworkObserver observer,
    FieldNetworkRemediationExecutor executor,
    FieldNetworkMutationCommandBuilder commandBuilder,
    IFieldNetworkMutationAuthority mutationAuthority)
    : IFieldNetworkRemediationOrchestrator
{
    public async Task<FieldNetworkRemediationOrchestrationResult> RemediateAsync(
        CancellationToken ct = default)
    {
        var profile = FieldNetworkProfile.Canonical;

        var initialObservation = await observer.ObserveAsync(ct);
        var initialState = FieldNetworkStateEvaluator.Evaluate(
            profile,
            initialObservation);
        var plan = FieldNetworkRemediationPlanner.Plan(
            profile,
            initialObservation);

        if (plan.Disposition == FieldNetworkRemediationDisposition.NoAction)
        {
            return Result(
                FieldNetworkRemediationOrchestrationStatus.NoAction,
                plan.Reason,
                initialState,
                plan,
                null,
                Array.Empty<FieldNetworkMutationCommand>(),
                null,
                initialState);
        }

        if (plan.Disposition == FieldNetworkRemediationDisposition.Refused)
        {
            return Result(
                FieldNetworkRemediationOrchestrationStatus.Refused,
                plan.Reason,
                initialState,
                plan,
                null,
                Array.Empty<FieldNetworkMutationCommand>(),
                null,
                initialState);
        }

        // Re-observe immediately before the dry-run. The executor independently
        // recomputes the plan and rejects stale or changed machine state.
        var freshObservation = await observer.ObserveAsync(ct);
        var dryRun = executor.ExecuteDryRun(
            profile,
            freshObservation,
            plan);

        if (dryRun.Status != FieldNetworkRemediationExecutionStatus.DryRunVerified)
        {
            var refusedState = FieldNetworkStateEvaluator.Evaluate(
                profile,
                freshObservation);
            return Result(
                FieldNetworkRemediationOrchestrationStatus.Refused,
                dryRun.Reason,
                initialState,
                plan,
                dryRun,
                Array.Empty<FieldNetworkMutationCommand>(),
                null,
                refusedState);
        }

        var commands = commandBuilder.Build(
            profile,
            freshObservation,
            dryRun);

        var mutation = await mutationAuthority.ExecuteAsync(
            commands,
            ct);

        var recoveredObservation = await observer.ObserveAsync(ct);
        var finalState = FieldNetworkStateEvaluator.Evaluate(
            profile,
            recoveredObservation);

        if (mutation.Status != FieldNetworkMutationExecutionStatus.Applied)
        {
            return Result(
                FieldNetworkRemediationOrchestrationStatus.Failed,
                mutation.Reason,
                initialState,
                plan,
                dryRun,
                commands,
                mutation,
                finalState);
        }

        var postPlan = FieldNetworkRemediationPlanner.Plan(
            profile,
            recoveredObservation);

        if (postPlan.Disposition != FieldNetworkRemediationDisposition.NoAction ||
            !finalState.LocalArchitectureReady)
        {
            return Result(
                FieldNetworkRemediationOrchestrationStatus.Failed,
                "Mutation completed but canonical field-network recovery was not verified.",
                initialState,
                plan,
                dryRun,
                commands,
                mutation,
                finalState);
        }

        return Result(
            FieldNetworkRemediationOrchestrationStatus.Applied,
            "Canonical field-network remediation applied and verified by fresh observation.",
            initialState,
            plan,
            dryRun,
            commands,
            mutation,
            finalState);
    }

    private static FieldNetworkRemediationOrchestrationResult Result(
        FieldNetworkRemediationOrchestrationStatus status,
        string reason,
        FieldNetworkState initialState,
        FieldNetworkRemediationPlan plan,
        FieldNetworkRemediationExecutionResult? dryRun,
        IReadOnlyList<FieldNetworkMutationCommand> commands,
        FieldNetworkMutationExecutionResult? mutation,
        FieldNetworkState finalState) =>
        new(
            DateTimeOffset.UtcNow,
            status,
            reason,
            initialState,
            plan,
            dryRun,
            commands,
            mutation,
            finalState);
}