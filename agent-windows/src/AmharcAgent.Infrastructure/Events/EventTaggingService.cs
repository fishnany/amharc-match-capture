using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AmharcAgent.Infrastructure.Events;

public class EventTaggingService(
    IEventRepository events,
    IMatchRepository matches,
    IScoringService scoring,
    IOverlayService overlay,
    IBroadcastService broadcast,
    ILogger<EventTaggingService> logger) : IEventTaggingService
{
    private static readonly HashSet<string> ScoreEvents =
    [
        "goal", "point", "penalty_goal", "penalty-goal",
        "two-point", "two_point", "two-point-score"
    ];

    public async Task<MatchEvent> CreateEventAsync(CreateEventOptions opts, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(opts.MatchId, ct);
        var beforeState = match is not null ? scoring.GetSnapshot(match) : null;

        if (match is not null && ScoreEvents.Contains(opts.EventType.ToLowerInvariant()))
        {
            if (opts.Team is null)
                throw new InvalidOperationException("Scoring events require a team.");

            scoring.Apply(match, opts.Team.Value, opts.EventType, 1);
            await matches.UpdateAsync(match, ct);

            var scoreState = scoring.GetState(match);
            overlay.UpdateScore(scoreState);
            broadcast.UpdateScore(scoreState);
        }

        var afterState = match is not null ? scoring.GetSnapshot(match) : null;

        var evt = new MatchEvent
        {
            EventId = Guid.NewGuid().ToString(),
            MatchId = opts.MatchId,
            EventType = opts.EventType,
            Team = opts.Team,
            PlayerNumber = opts.PlayerNumber,
            Period = opts.Period,
            MatchClockSeconds = opts.MatchClockSeconds,
            RecordingElapsedSeconds = opts.RecordingElapsedSeconds,
            SystemTimestamp = DateTimeOffset.UtcNow,
            Source = opts.Source,
            Operator = opts.Operator,
            Note = opts.Note,
            ScoreBeforeState = beforeState,
            ScoreAfterState = afterState,
            ScoreBefore = beforeState?.Display,
            ScoreAfter = afterState?.Display,
            ClipRequested = opts.ClipRequested,
            ReviewStatus = ReviewStatus.Unreviewed
        };

        var created = await events.CreateAsync(evt, ct);

        // Phase 1.2: event creation is the single trigger for transient broadcast graphics.
        broadcast.ShowGraphic(NormalizeGraphicType(created.EventType), GraphicDurationMs(created.EventType));

        logger.LogInformation("Event created: {Type} at {Clock}s (rec: {Rec}s)",
            opts.EventType, opts.MatchClockSeconds, opts.RecordingElapsedSeconds);
        return created;
    }

    public async Task<MatchEvent> UpdateEventAsync(string eventId, MatchEvent updates, CancellationToken ct = default)
    {
        var existing = await events.GetByIdAsync(eventId, ct)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");
        existing.Note = updates.Note;
        existing.ReviewStatus = updates.ReviewStatus;
        existing.ClipRequested = updates.ClipRequested;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        return await events.UpdateAsync(existing, ct);
    }

    public Task DeleteEventAsync(string eventId, CancellationToken ct = default) =>
        events.DeleteAsync(eventId, ct);

    public async Task<MatchEvent?> UndoLastEventAsync(string matchId, CancellationToken ct = default)
    {
        var last = await events.GetLastEventAsync(matchId, ct);
        if (last is null) return null;
        await events.DeleteAsync(last.EventId, ct);
        logger.LogInformation("Undid last event: {Type}", last.EventType);
        return last;
    }

    public async Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, CancellationToken ct = default) =>
        await events.GetByMatchIdAsync(matchId, ct);

    public async Task<string> ExportEventsJsonAsync(string matchId, CancellationToken ct = default)
    {
        var evts = await GetEventsAsync(matchId, ct);
        return System.Text.Json.JsonSerializer.Serialize(evts,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExportEventsCsvAsync(string matchId, CancellationToken ct = default)
    {
        var evts = await GetEventsAsync(matchId, ct);
        var sb = new StringBuilder();
        sb.AppendLine("EventId,EventType,Team,PlayerNumber,Period,MatchClockSeconds,RecordingElapsedSeconds,SystemTimestamp,Source,Note,ScoreBefore,ScoreAfter,ClipRequested,ReviewStatus");
        foreach (var e in evts)
            sb.AppendLine($"{e.EventId},{e.EventType},{e.Team},{e.PlayerNumber},{e.Period},{e.MatchClockSeconds},{e.RecordingElapsedSeconds},{e.SystemTimestamp:O},{e.Source},{Quote(e.Note)},{Quote(e.ScoreBefore)},{Quote(e.ScoreAfter)},{e.ClipRequested},{e.ReviewStatus}");
        return sb.ToString();
    }

    private static string NormalizeGraphicType(string value) => value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

    private static int GraphicDurationMs(string eventType) => NormalizeGraphicType(eventType) switch
    {
        "goal" or "penalty_goal" or "two_point" or "two_point_score" => 2500,
        "point" or "wide" or "short" or "free" or "free_awarded" or "mark" => 2000,
        "yellow_card" or "black_card" or "red_card" => 3000,
        "substitution" => 4000,
        "half_time" or "full_time" => 5000,
        _ => 2200
    };

    private static string Quote(string? s) => s is null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
}
