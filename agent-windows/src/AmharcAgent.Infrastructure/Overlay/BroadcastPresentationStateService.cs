using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;

namespace AmharcAgent.Infrastructure.Overlay;

/// <summary>
/// Composes broadcast presentation state from authoritative AMHARC domain
/// services. It does not independently calculate score or advance the clock.
/// </summary>
public sealed class BroadcastPresentationStateService(
    IMatchRepository matches,
    IScoringService scoring,
    ICanonicalClockSnapshotService clockSnapshots,
    IOverlayService overlay)
    : IBroadcastPresentationStateService
{
    public async Task<BroadcastPresentationStateV1> CreateStateAsync(
        string matchId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        var match =
            await matches.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
        {
            throw new InvalidOperationException(
                $"Cannot create broadcast presentation state because " +
                $"match '{matchId}' does not exist.");
        }

        var score =
            scoring.GetState(match);

        var clock =
            clockSnapshots.CreateSnapshot(matchId);

        return BroadcastPresentationStateV1.FromCanonical(
            score,
            clock,
            overlay.State);
    }
}
