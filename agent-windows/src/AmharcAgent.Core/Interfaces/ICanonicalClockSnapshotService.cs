using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Produces canonical AMHARC ClockSnapshot v1 instances
/// from the current Capture runtime clock authority.
/// </summary>
public interface ICanonicalClockSnapshotService
{
    ClockSnapshotV1 CreateSnapshot(string matchId);
}
