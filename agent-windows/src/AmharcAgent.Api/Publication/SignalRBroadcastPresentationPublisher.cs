using AmharcAgent.Api.Hubs;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AmharcAgent.Api.Publication;

/// <summary>
/// Publishes the complete canonical BroadcastPresentationStateV1 contract to
/// consumers subscribed to the existing per-match SignalR group.
///
/// This publisher deliberately emits one complete presentation snapshot rather
/// than renderer-facing score, clock or overlay deltas.
/// </summary>
public sealed class SignalRBroadcastPresentationPublisher(
    IBroadcastPresentationStateService presentationState,
    IHubContext<MatchHub> hubContext,
    ILogger<SignalRBroadcastPresentationPublisher> logger)
    : IBroadcastPresentationPublisher
{
    public const string ClientMethod =
        "BroadcastPresentationUpdated";

    public async Task<BroadcastPresentationStateV1> PublishAsync(
        string matchId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            matchId);

        var state =
            await presentationState.CreateStateAsync(
                matchId,
                ct);

        await hubContext.Clients
            .Group($"match:{matchId}")
            .SendAsync(
                ClientMethod,
                state,
                ct);

        logger.LogDebug(
            "Published canonical BroadcastPresentationState v{ContractVersion} for match {MatchId}: clockSequence={Sequence}, authorityEpoch={AuthorityEpoch}, scoreboardVisible={ScoreboardVisible}, outputMode={OutputMode}",
            state.ContractVersion,
            state.MatchId,
            state.Clock.Sequence,
            state.Clock.AuthorityEpoch,
            state.Presentation.ScoreboardVisible,
            state.Presentation.OutputMode);

        return state;
    }
}
