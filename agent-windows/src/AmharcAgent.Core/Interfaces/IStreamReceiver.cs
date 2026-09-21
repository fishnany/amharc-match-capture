using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Canonical runtime boundary for receiving and supervising the live camera
/// video stream. The receiver owns media availability and health; it does not
/// expose authenticated RTSP credentials or a browser-facing transport.
/// </summary>
public interface IStreamReceiver
{
    StreamReceiverState State { get; }
    StreamReceiverHealth Health { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task RecoverAsync(CancellationToken ct = default);

    event Action<StreamReceiverState> StateChanged;
    event Action<StreamReceiverHealth> HealthChanged;
}
