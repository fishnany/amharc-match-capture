using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Explicit bounded remediation entry point for the canonical AMHARC field network.
/// Readiness observation MUST NOT invoke this service.
/// </summary>
public interface IFieldNetworkRemediationOrchestrator
{
    Task<FieldNetworkRemediationOrchestrationResult> RemediateAsync(
        CancellationToken ct = default);
}