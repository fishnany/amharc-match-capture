using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Publishes canonical AMHARC ClockSnapshot v1 instances produced by Capture.
///
/// This abstraction represents canonical snapshot publication only.
/// It does not define Tagger SYNC_CLOCK transport or consumer behaviour.
/// </summary>
public interface IClockSnapshotPublisher
{
    Task<ClockSnapshotV1> PublishAsync(
        string matchId,
        CancellationToken ct = default);
}
