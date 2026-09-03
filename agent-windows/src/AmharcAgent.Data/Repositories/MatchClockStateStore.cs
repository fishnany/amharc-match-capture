using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

public class MatchClockStateStore(
    IDbContextFactory<AmharcDbContext> dbFactory)
    : IMatchClockStateStore
{
    public async Task<MatchClockRuntimeState?> LoadAsync(
        string matchId,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        return await db.MatchClockRuntimeStates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.MatchId == matchId,
                ct);
    }

    public async Task SaveAsync(
        MatchClockRuntimeState state,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        var existing =
            await db.MatchClockRuntimeStates
                .FirstOrDefaultAsync(
                    c => c.MatchId == state.MatchId,
                    ct);

        if (existing is null)
        {
            db.MatchClockRuntimeStates.Add(state);
        }
        else
        {
            existing.MatchClockSeconds =
                state.MatchClockSeconds;

            existing.RecordingElapsedSeconds =
                state.RecordingElapsedSeconds;

            existing.IsRunning =
                state.IsRunning;

            existing.CurrentPeriod =
                state.CurrentPeriod;

            existing.ClockMode =
                state.ClockMode;

            existing.PersistedAt =
                state.PersistedAt;

            existing.UpdatedAt =
                DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        string matchId,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        var existing =
            await db.MatchClockRuntimeStates
                .FirstOrDefaultAsync(
                    c => c.MatchId == matchId,
                    ct);

        if (existing is null)
            return;

        db.MatchClockRuntimeStates.Remove(existing);

        await db.SaveChangesAsync(ct);
    }
}