using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Provides read-only observation of the AMHARC field network.
///
/// Implementations MUST NOT change adapter configuration, addresses,
/// gateways, routes, DNS settings, interface metrics, or other operating
/// system network state while performing ObserveAsync.
/// </summary>
public interface IFieldNetworkService
{
    Task<FieldNetworkState> ObserveAsync(
        CancellationToken ct = default);
}
