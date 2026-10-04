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

        if (clock is not ISubjectBoundClockService bound)
            throw new InvalidOperationException("CLOCK_SUBJECT_CONTEXT_UNAVAILABLE");
        // Sequence allocation is inside the same consequential read boundary.
        // A wrong subject cannot consume an order or publish relabelled state.
        return bound.ReadFor(matchId, state =>
            ClockSnapshotV1Adapter.FromClockState(
                state, matchId, authorityContext.Authority,
                authorityContext.AuthorityEpoch, authorityContext.NextSequence()));
    }
}
