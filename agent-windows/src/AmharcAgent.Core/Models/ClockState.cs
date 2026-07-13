namespace AmharcAgent.Core.Models;

/// <summary>
/// Snapshot of the dual-clock state.
/// MatchClockSeconds and RecordingElapsedSeconds are ALWAYS independent values.
/// </summary>
public record ClockState(
    int MatchClockSeconds,
    int RecordingElapsedSeconds,
    bool IsRunning,
    int CurrentPeriod,
    string ClockMode,
    DateTimeOffset UpdatedAt);

public record ClockCorrectionEntry(
    DateTimeOffset CorrectedAt,
    int PreviousMatchClockSeconds,
    int NewMatchClockSeconds,
    string? Reason,
    string? Operator);
