using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

public sealed class RecordingSessionStore(
    IDbContextFactory<AmharcDbContext> dbFactory)
    : IRecordingSessionStore
{
    public async Task<RecordingSession?> GetByIdAsync(
        string recordingId,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        return await db.RecordingSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.RecordingId == recordingId,
                ct);
    }

    public async Task<RecordingSession?> GetActiveForMatchAsync(
        string matchId,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        return await db.RecordingSessions
            .AsNoTracking()
            .Where(r =>
                r.MatchId == matchId &&
                (r.State == RecordingState.Starting ||
                 r.State == RecordingState.Recording ||
                 r.State == RecordingState.Rotating ||
                 r.State == RecordingState.Recovering))
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RecordingSession?> GetRecoverableAsync(
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        return await db.RecordingSessions
            .AsNoTracking()
            .Where(r =>
                r.State == RecordingState.Starting ||
                r.State == RecordingState.Recording ||
                r.State == RecordingState.Rotating ||
                r.State == RecordingState.Recovering)
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(
        RecordingSession session,
        CancellationToken ct = default)
    {
        await using var db =
            await dbFactory.CreateDbContextAsync(ct);

        var existing =
            await db.RecordingSessions
                .FirstOrDefaultAsync(
                    r => r.RecordingId == session.RecordingId,
                    ct);

        if (existing is null)
        {
            db.RecordingSessions.Add(session);
        }
        else
        {
            existing.MatchId = session.MatchId;
            existing.CameraId = session.CameraId;
            existing.State = session.State;
            existing.OutputDirectory = session.OutputDirectory;
            existing.RtspUrl = session.RtspUrl;
            existing.StartedAt = session.StartedAt;
            existing.StoppedAt = session.StoppedAt;
            existing.SegmentDurationSeconds =
                session.SegmentDurationSeconds;
            existing.IncludeAudio =
                session.IncludeAudio;
            existing.SegmentCount =
                session.SegmentCount;
            existing.FinalFilePath =
                session.FinalFilePath;
            existing.Checksum =
                session.Checksum;
            existing.UpdatedAt =
                DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}