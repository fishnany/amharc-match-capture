namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Consumer-facing media boundary for canonical live-video ingress.
/// Consumers acquire credential-free media leases and never receive the
/// authenticated camera source.
/// </summary>
public interface IStreamReceiverMediaSource
{
    Task<IStreamReceiverMediaLease> AcquireAsync(
        CancellationToken ct = default,
        bool lossIntolerant = false);
}