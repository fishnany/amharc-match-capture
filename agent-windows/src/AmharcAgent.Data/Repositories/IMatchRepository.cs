using AmharcAgent.Core.Domain;

namespace AmharcAgent.Data.Repositories;

/// <summary>Repository contract for <see cref="Match"/> persistence.</summary>
public interface IMatchRepository
{
    /// <summary>Returns all matches, ordered by date descending.</summary>
    Task<IEnumerable<Match>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the match with the specified identifier, or null if not found.</summary>
    Task<Match?> GetByIdAsync(string matchId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new match to the store.</summary>
    Task AddAsync(Match match, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing match.</summary>
    Task UpdateAsync(Match match, CancellationToken cancellationToken = default);

    /// <summary>Removes the match with the specified identifier.</summary>
    Task DeleteAsync(string matchId, CancellationToken cancellationToken = default);
}
