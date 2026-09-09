using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Publishes the complete canonical AMHARC broadcast presentation contract.
///
/// Transport consumers receive BroadcastPresentationStateV1 as one authoritative
/// snapshot. They must not reconstruct broadcast state from separate score,
/// clock or overlay messages.
/// </summary>
public interface IBroadcastPresentationPublisher
{
    Task<BroadcastPresentationStateV1> PublishAsync(
        string matchId,
        CancellationToken ct = default);
}
