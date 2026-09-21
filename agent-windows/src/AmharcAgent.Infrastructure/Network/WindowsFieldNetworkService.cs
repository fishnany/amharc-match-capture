using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Read-only Windows implementation of the canonical AMHARC field-network
/// state service. Raw Windows acquisition is delegated to the authoritative
/// WindowsFieldNetworkObserver so readiness and remediation can consume the
/// same observation source.
/// </summary>
internal sealed class WindowsFieldNetworkService : IFieldNetworkService
{
    private readonly IWindowsFieldNetworkObserver _observer;

    public WindowsFieldNetworkService(
        IWindowsFieldNetworkObserver observer)
    {
        _observer = observer;
    }

    public async Task<FieldNetworkState> ObserveAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "AMHARC field-network observation is supported on Windows only.");
        }

        var observation =
            await _observer.ObserveAsync(ct);

        return FieldNetworkStateEvaluator.Evaluate(
            FieldNetworkProfile.Canonical,
            observation);
    }
}