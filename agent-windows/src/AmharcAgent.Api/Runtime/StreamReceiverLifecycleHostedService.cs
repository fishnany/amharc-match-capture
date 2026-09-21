using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Hosting;

namespace AmharcAgent.Api.Runtime;

/// <summary>
/// Establishes explicit host ownership of the canonical stream receiver
/// lifecycle while preserving lazy canonical media ingress.
///
/// Starting the host does not start canonical camera acquisition. Canonical
/// ingress remains demand-started by IStreamReceiverMediaSource consumers.
///
/// Host shutdown explicitly and asynchronously stops the canonical receiver,
/// ensuring that receiver-owned media channels, pumps and FFmpeg resources
/// are released before host shutdown completes.
/// </summary>
public sealed class StreamReceiverLifecycleHostedService(
    IStreamReceiver streamReceiver)
    : IHostedService
{
    /// <summary>
    /// Preserves the existing lazy-ingress contract. Merely starting the
    /// application does not acquire the canonical camera stream.
    /// </summary>
    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Establishes the host as the authoritative shutdown owner for canonical
    /// media ingress and awaits deterministic receiver shutdown.
    /// </summary>
    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return streamReceiver.StopAsync(
            cancellationToken);
    }
}