using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Discovery;

/// <summary>Stub camera discovery service (Phase 1 – returns empty list).</summary>
public sealed class CameraDiscoveryService : ICameraDiscoveryService
{
    private readonly ILogger<CameraDiscoveryService> _logger;

    public CameraDiscoveryService(ILogger<CameraDiscoveryService> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Camera discovery (stub) — no cameras found");
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
