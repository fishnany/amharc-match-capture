using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Maps the Capture runtime clock state to the canonical AMHARC
/// ClockSnapshot v1 interoperability contract.
/// </summary>
public static class ClockSnapshotV1Adapter
{
    public const string ContractVersion = "1.0";

    public static ClockSnapshotV1 FromClockState(
        ClockState state,
        string matchId,
        ClockAuthorityV1 authority,
        long authorityEpoch,
        long sequence)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentNullException.ThrowIfNull(authority);

        var totalMatchElapsedSeconds =
            state.MatchClockSeconds;

        var periodStartTotalMatchElapsedSeconds =
            state.PeriodStartTotalMatchElapsedSeconds;

        if (
            periodStartTotalMatchElapsedSeconds is null &&
            state.CurrentPeriod > 1)
        {
            throw new InvalidOperationException(
                "Cannot create canonical ClockSnapshot v1 because the current period boundary is unknown.");
        }

        var periodStartBoundary =
            periodStartTotalMatchElapsedSeconds ?? 0;

        var periodClockSeconds =
            Math.Max(
                0,
                totalMatchElapsedSeconds -
                periodStartBoundary);

        return new ClockSnapshotV1(
            ContractVersion: ContractVersion,
            MatchId: matchId,
            Period: state.CurrentPeriod,
            PeriodClockSeconds: periodClockSeconds,
            TotalMatchElapsedSeconds: totalMatchElapsedSeconds,
            IsRunning: state.IsRunning,
            RecordingElapsedSeconds:
                state.RecordingElapsedSeconds,
            ObservedAtUtc: state.UpdatedAt,
            Authority: authority,
            AuthorityEpoch: authorityEpoch,
            Sequence: sequence);
    }
}
