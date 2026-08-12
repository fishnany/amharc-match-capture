using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

public interface IEventRepository
{
    Task<IReadOnlyList<MatchEvent>> GetByMatchIdAsync(
        string matchId,
        CancellationToken ct = default);

    Task<MatchEvent?> GetByIdAsync(
        string eventId,
        CancellationToken ct = default);

    Task<MatchEvent> CreateAsync(
        MatchEvent evt,
        CancellationToken ct = default);

    Task<MatchEvent> UpdateAsync(
        MatchEvent evt,
        CancellationToken ct = default);

    Task DeleteAsync(
        string eventId,
        CancellationToken ct = default);

    Task<MatchEvent?> GetLastEventAsync(
        string matchId,
        CancellationToken ct = default);
}

public class EventRepository(AmharcDbContext db) : IEventRepository
{
    public async Task<IReadOnlyList<MatchEvent>> GetByMatchIdAsync(
        string matchId,
        CancellationToken ct = default)
    {
        var events = await db.MatchEvents
            .Where(e => e.MatchId == matchId)
            .ToListAsync(ct);

        return events
            .OrderBy(e => e.SystemTimestamp)
            .ToList();
    }

    public async Task<MatchEvent?> GetByIdAsync(
        string eventId,
        CancellationToken ct = default) =>
        await db.MatchEvents.FindAsync([eventId], ct);

    public async Task<MatchEvent?> GetLastEventAsync(
        string matchId,
        CancellationToken ct = default)
    {
        var events = await db.MatchEvents
            .Where(e => e.MatchId == matchId)
            .ToListAsync(ct);

        return events
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<MatchEvent> CreateAsync(
        MatchEvent evt,
        CancellationToken ct = default)
    {
        db.MatchEvents.Add(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }

    public async Task<MatchEvent> UpdateAsync(
        MatchEvent evt,
        CancellationToken ct = default)
    {
        db.MatchEvents.Update(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }

    public async Task DeleteAsync(
        string eventId,
        CancellationToken ct = default)
    {
        var evt = await GetByIdAsync(eventId, ct);

        if (evt is not null)
        {
            db.MatchEvents.Remove(evt);
            await db.SaveChangesAsync(ct);
        }
    }
}