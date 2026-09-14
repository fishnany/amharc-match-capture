namespace AmharcAgent.Core.Domain;

/// <summary>
/// Lifecycle state of the canonical live-video stream receiver.
/// Camera connectivity and media availability are intentionally separate concerns.
/// </summary>
public enum StreamReceiverState
{
    Idle,
    Starting,
    Available,
    Interrupted,
    Recovering,
    Stopping,
    Error
}
