using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>
/// Produces canonical ClockSnapshot v1 instances from the authoritative
/// Capture runtime clock and process-lifetime authority context.
/// </summary>
public sealed class CanonicalClockSnapshotService(
    IMatchClockService clock,
    IClockAuthorityContext authorityContext)
    : ICanonicalClockSnapshotService
{
    public ClockSnapshotV1 CreateSnapshot(
        string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        var state =
            clock.State;

        var sequence =
            authorityContext.NextSequence();

        return ClockSnapshotV1Adapter.FromClockState(
            state,
            matchId,
            authorityContext.Authority,
            authorityContext.AuthorityEpoch,
            sequence);
    }
}
