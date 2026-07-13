namespace AmharcAgent.Core.Models;

/// <summary>Source of a match event.</summary>
public enum EventSource
{
    OperatorUi,
    StreamDeck,
    Joystick,
    System,
    Imported,
    Api,
    Automatic
}

/// <summary>Review status of a match event.</summary>
public enum ReviewStatus
{
    Unreviewed,
    Reviewed,
    Corrected,
    Rejected,
    Flagged
}

/// <summary>A tagged match event stored in the database.</summary>
public class MatchEvent
{
    public Guid EventId { get; set; }
    public string MatchId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Team { get; set; }
    public string? PlayerId { get; set; }
    public int? PlayerNumber { get; set; }
    public int Period { get; set; }
    public double MatchClockSeconds { get; set; }
    public double RecordingElapsedSeconds { get; set; }
    public DateTimeOffset SystemTimestamp { get; set; }
    public EventSource Source { get; set; }
    public string? Operator { get; set; }
    public string? Note { get; set; }
    public string? ScoreBefore { get; set; }
    public string? ScoreAfter { get; set; }
    public bool ClipRequested { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Unreviewed;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Options for creating a new match event.</summary>
public record CreateEventOptions(
    string MatchId,
    string EventType,
    string? Team,
    int? PlayerNumber,
    int Period,
    double MatchClockSeconds,
    double RecordingElapsedSeconds,
    EventSource Source,
    string? Note = null,
    bool ClipRequested = false);
