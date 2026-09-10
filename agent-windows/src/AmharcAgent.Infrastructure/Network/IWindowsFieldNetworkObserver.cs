namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Internal authoritative raw-observation seam used by both readiness and remediation.
/// It MUST remain read-only.
/// </summary>
internal interface IWindowsFieldNetworkObserver
{
    Task<WindowsFieldNetworkObservation> ObserveAsync(
        CancellationToken ct = default);
}