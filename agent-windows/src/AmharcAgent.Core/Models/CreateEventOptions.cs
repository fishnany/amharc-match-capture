using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Models;

public record CreateEventOptions(
    string MatchId,
    string EventType,
    EventTeam? Team,
    int? PlayerNumber,
    int Period,
    int MatchClockSeconds,
    int RecordingElapsedSeconds,
    EventSource Source,
    string? Note = null,
    bool ClipRequested = false,
    string? Operator = null);
