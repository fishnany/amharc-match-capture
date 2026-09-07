namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Canonical AMHARC temporal interoperability contract.
/// Semantics are governed by ClockSnapshot v1 in gaelic-games-ontology.
/// </summary>
public sealed record ClockSnapshotV1(
    string ContractVersion,
    string MatchId,
    int Period,
    int PeriodClockSeconds,
    int TotalMatchElapsedSeconds,
    bool IsRunning,
    int? RecordingElapsedSeconds,
    DateTimeOffset ObservedAtUtc,
    ClockAuthorityV1 Authority,
    long AuthorityEpoch,
    long Sequence);
