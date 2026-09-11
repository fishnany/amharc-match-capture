namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Executes one bounded authoritative audio runtime health observation.
/// Scheduling belongs to the application host.
/// </summary>
public interface IAudioRuntimeHealthObserver
{
    Task ObserveOnceAsync(
        CancellationToken cancellationToken = default);
}
