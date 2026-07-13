namespace AmharcAgent.Core.Domain;

/// <summary>Who or what created the event.</summary>
public enum EventSource { OperatorUi, StreamDeck, Joystick, System, Imported, Api, Automatic }

/// <summary>Post-match review status of an event.</summary>
public enum ReviewStatus { Unreviewed, Reviewed, Corrected, Rejected, Flagged }

/// <summary>Which team an event is attributed to.</summary>
public enum EventTeam { Home, Away }

/// <summary>
/// A tagged event during a match (score, card, substitution, etc.).
/// CRITICAL: MatchClockSeconds and RecordingElapsedSeconds are ALWAYS independent values.
/// </summary>
public class MatchEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string MatchId { get; set; } = string.Empty;

    /// <summary>
    /// Event type string: point, goal, wide, yellow_card, red_card, black_card,
    /// free_awarded, 50m_free, penalty_goal, penalty_miss, substitution,
    /// half_time, full_time, sideline_cut, highlight.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public EventTeam? Team { get; set; }
    public string? PlayerId { get; set; }
    public int? PlayerNumber { get; set; }
    public int Period { get; set; }

    /// <summary>Official match clock time in seconds (can be paused/corrected).</summary>
    public int MatchClockSeconds { get; set; }

    /// <summary>Continuous recording elapsed time in seconds (never paused — independent of match clock).</summary>
    public int RecordingElapsedSeconds { get; set; }

    public DateTimeOffset SystemTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public EventSource Source { get; set; } = EventSource.OperatorUi;
    public string? Operator { get; set; }
    public string? Note { get; set; }

    /// <summary>Score before this event in goals-points format (e.g. "1-12").</summary>
    public string? ScoreBefore { get; set; }
    /// <summary>Score after this event in goals-points format.</summary>
    public string? ScoreAfter { get; set; }

    public bool ClipRequested { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Unreviewed;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
