namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Requests asynchronous publication of Capture's current canonical
/// ClockSnapshot v1 for a known match.
///
/// Requests are scheduling signals only. Domain-command success must not
/// depend on downstream transport delivery.
///
/// This abstraction does not define Tagger SYNC_CLOCK behaviour.
/// </summary>
public interface IClockSnapshotPublicationScheduler
{
    void RequestPublication(
        string matchId);
}
