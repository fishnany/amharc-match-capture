using AmharcAgent.Api.Hubs;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AmharcAgent.Api.Publication;

/// <summary>
/// Publishes Capture's canonical ClockSnapshot v1 contract to consumers
/// subscribed to the existing per-match SignalR group.
///
/// Transport-specific publication is deliberately separated from
/// Tagger's SYNC_CLOCK consumer/transport implementation.
/// </summary>
public sealed class SignalRClockSnapshotPublisher(
    ICanonicalClockSnapshotService snapshotService,
    IHubContext<MatchHub> hubContext,
    ILogger<SignalRClockSnapshotPublisher> logger)
    : IClockSnapshotPublisher
{
    public const string ClientMethod =
        "ClockSnapshotUpdated";

    public async Task<ClockSnapshotV1> PublishAsync(
        string matchId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            matchId);

        var snapshot =
            snapshotService.CreateSnapshot(
                matchId);

        await hubContext.Clients
            .Group($"match:{matchId}")
            .SendAsync(
                ClientMethod,
                snapshot,
                ct);

        logger.LogDebug(
            "Published canonical ClockSnapshot v1 for match {MatchId}: authorityEpoch={AuthorityEpoch}, sequence={Sequence}, totalMatchElapsed={TotalMatchElapsedSeconds}, period={Period}, periodClock={PeriodClockSeconds}, running={IsRunning}",
            snapshot.MatchId,
            snapshot.AuthorityEpoch,
            snapshot.Sequence,
            snapshot.TotalMatchElapsedSeconds,
            snapshot.Period,
            snapshot.PeriodClockSeconds,
            snapshot.IsRunning);

        return snapshot;
    }
}
