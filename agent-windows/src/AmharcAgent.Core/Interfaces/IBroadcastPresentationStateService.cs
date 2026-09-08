using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Composes the authoritative state consumed by the AMHARC broadcast
/// presentation layer.
/// </summary>
public interface IBroadcastPresentationStateService
{
    Task<BroadcastPresentationStateV1> CreateStateAsync(
        string matchId,
        CancellationToken ct = default);
}
