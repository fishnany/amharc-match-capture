using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Creates and manages match events with dual-clock timestamps.</summary>
public interface IEventTaggingService
{
    Task<MatchEvent> CreateEventAsync(CreateEventOptions options, CancellationToken ct = default);
    Task<MatchEvent> UpdateEventAsync(string eventId, MatchEvent updates, CancellationToken ct = default);
    Task DeleteEventAsync(string eventId, CancellationToken ct = default);
    Task<MatchEvent?> UndoLastEventAsync(string matchId, CancellationToken ct = default);
    Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, CancellationToken ct = default);
    Task<string> ExportEventsJsonAsync(string matchId, CancellationToken ct = default);
    Task<string> ExportEventsCsvAsync(string matchId, CancellationToken ct = default);
}
