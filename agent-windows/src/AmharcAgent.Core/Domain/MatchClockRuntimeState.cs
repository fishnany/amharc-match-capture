namespace AmharcAgent.Core.Domain;

/// <summary>
/// Durable recovery checkpoint for the canonical dual-clock state of a match.
/// Exactly one runtime state exists per match.
/// </summary>
public class MatchClockRuntimeState
{
    /// <summary>
    /// Canonical match identifier. Also serves as the primary key.
    /// </summary>
    public string MatchId { get; set; } = string.Empty;

    /// <summary>
    /// Official match-clock position at the persistence checkpoint.
    /// </summary>
    public int MatchClockSeconds { get; set; }

    /// <summary>
    /// Continuous recording/capture elapsed position at the persistence checkpoint.
    /// This remains independent of the official match clock.
    /// </summary>
    public int RecordingElapsedSeconds { get; set; }

    /// <summary>
    /// Whether the official match clock was running at the checkpoint.
    /// </summary>
    public bool IsRunning { get; set; }

    public int CurrentPeriod { get; set; }

    public string ClockMode { get; set; } = "count-up";

    /// <summary>
    /// UTC timestamp against which elapsed time is reconstructed after restart.
    /// </summary>
    public DateTimeOffset PersistedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } =
        DateTimeOffset.UtcNow;
}