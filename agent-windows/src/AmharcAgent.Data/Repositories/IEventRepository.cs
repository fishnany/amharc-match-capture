using AmharcAgent.Core.Domain;

namespace AmharcAgent.Data.Repositories;

/// <summary>Repository contract for <see cref="MatchEvent"/> persistence.</summary>
public interface IEventRepository
{
    /// <summary>Returns all events for the specified match in chronological order.</summary>
    Task<IEnumerable<MatchEvent>> GetByMatchIdAsync(string matchId, CancellationToken cancellationToken = default);

    /// <summary>Returns all events for the specified match and period in chronological order.</summary>
    Task<IEnumerable<MatchEvent>> GetByMatchIdAndPeriodAsync(string matchId, int period, CancellationToken cancellationToken = default);

    /// <summary>Returns the event with the specified identifier, or null if not found.</summary>
    Task<MatchEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new event to the store.</summary>
    Task AddAsync(MatchEvent matchEvent, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing event.</summary>
    Task UpdateAsync(MatchEvent matchEvent, CancellationToken cancellationToken = default);

    /// <summary>Removes the event with the specified identifier.</summary>
    Task DeleteAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the most recently created event for the specified match.
    /// Returns the removed event, or null if no events exist.
    /// </summary>
    Task<MatchEvent?> UndoLastAsync(string matchId, CancellationToken cancellationToken = default);
}
