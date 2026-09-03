using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Interfaces;

public interface IMatchClockStateStore
{
    Task<MatchClockRuntimeState?> LoadAsync(
        string matchId,
        CancellationToken ct = default);

    Task SaveAsync(
        MatchClockRuntimeState state,
        CancellationToken ct = default);

    Task DeleteAsync(
        string matchId,
        CancellationToken ct = default);
}
