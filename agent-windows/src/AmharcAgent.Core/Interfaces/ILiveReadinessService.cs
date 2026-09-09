using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Composes live-match operational readiness from authoritative subsystem
/// state without owning or mutating that state.
/// </summary>
public interface ILiveReadinessService
{
    Task<LiveReadinessStateV1> EvaluateAsync(
        string? matchId,
        CancellationToken ct = default);
}