namespace AmharcAgent.Core.Models;

/// <summary>Snapshot of the match clock state.</summary>
public record ClockState(
    double MatchClockSeconds,
    double RecordingElapsedSeconds,
    bool IsRunning,
    int CurrentPeriod,
    DateTimeOffset UpdatedAt);

/// <summary>Audit entry for a clock correction.</summary>
public record ClockCorrectionEntry(
    DateTimeOffset CorrectedAt,
    double PreviousMatchClockSeconds,
    double NewMatchClockSeconds,
    string? Reason,
    string? Operator);
