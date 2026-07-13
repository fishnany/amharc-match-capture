using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Scans the local network for AXIS cameras using parallel HTTP probes.</summary>
public interface ICameraDiscoveryService
{
    /// <summary>
    /// Scan a subnet for AXIS cameras.
    /// </summary>
    /// <param name="subnet">Subnet prefix e.g. "192.168.1". Null = auto-detect.</param>
    /// <param name="progress">Reports (found, scanned, total) as the scan proceeds.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<DiscoveredCamera>> ScanSubnetAsync(
        string? subnet,
        IProgress<(int Found, int Scanned, int Total)>? progress = null,
        CancellationToken ct = default);
}
