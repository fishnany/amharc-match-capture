using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Discovery;

/// <summary>
/// Stub camera discovery service for the current alpha release.
/// A future implementation will scan the selected subnet for AXIS cameras.
/// </summary>
public sealed class CameraDiscoveryService : ICameraDiscoveryService
{
    private readonly ILogger<CameraDiscoveryService> _logger;

    public CameraDiscoveryService(ILogger<CameraDiscoveryService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<DiscoveredCamera>> ScanSubnetAsync(
        string? subnet,
        IProgress<(int Found, int Scanned, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Camera discovery stub invoked for subnet {Subnet}; no cameras returned",
            subnet ?? "auto-detect");

        progress?.Report((Found: 0, Scanned: 0, Total: 0));

        return Task.FromResult<IEnumerable<DiscoveredCamera>>(
            Array.Empty<DiscoveredCamera>());
    }
}
