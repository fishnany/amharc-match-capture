using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken ct = default);
    Task<Match?> GetByIdAsync(string matchId, CancellationToken ct = default);
    Task<Match?> GetActiveMatchAsync(CancellationToken ct = default);
    Task<Match> CreateAsync(Match match, CancellationToken ct = default);
    Task<Match> UpdateAsync(Match match, CancellationToken ct = default);
    Task DeleteAsync(string matchId, CancellationToken ct = default);
}

public class MatchRepository(AmharcDbContext db) : IMatchRepository
{
    public async Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken ct = default) =>
        await db.Matches.OrderByDescending(m => m.Date).ToListAsync(ct);

    public async Task<Match?> GetByIdAsync(string matchId, CancellationToken ct = default) =>
        await db.Matches.FindAsync([matchId], ct);

    public async Task<Match?> GetActiveMatchAsync(
        CancellationToken ct = default)
    {
        var liveMatches =
        (await db.Matches
        .Where(m =>
            m.Status == MatchStatus.Active ||
            m.Status == MatchStatus.Paused ||
            m.Status == MatchStatus.HalfTime)
        .ToListAsync(ct))
    .OrderByDescending(m => m.UpdatedAt)
    .ToList();

        if (liveMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple operationally live matches were found ({liveMatches.Count}). " +
                "Expected at most one match in Active, Paused or HalfTime state.");
        }

        return liveMatches.SingleOrDefault();
    }

    public async Task<Match> CreateAsync(Match match, CancellationToken ct = default)
    {
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        return match;
    }

    public async Task<Match> UpdateAsync(Match match, CancellationToken ct = default)
    {
        db.Matches.Update(match);
        await db.SaveChangesAsync(ct);
        return match;
    }

    public async Task DeleteAsync(string matchId, CancellationToken ct = default)
    {
        var match = await GetByIdAsync(matchId, ct);
        if (match is not null)
        {
            db.Matches.Remove(match);
            await db.SaveChangesAsync(ct);
        }
    }
}
