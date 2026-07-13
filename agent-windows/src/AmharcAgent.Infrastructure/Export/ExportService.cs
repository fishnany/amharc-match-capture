using System.Text.Json;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Export;

public class ExportService(
    IMatchRepository matches,
    IEventRepository events,
    IRecordingService recording,
    ILogger<ExportService> logger) : IExportService
{
    private const string FormatName = "amharc-match-capture";
    private const int FormatVersion = 1;
    private const string AppVersion = "1.0.0";

    public async Task<string> ExportEventsJsonAsync(string matchId, CancellationToken ct = default)
    {
        var evts = await events.GetByMatchIdAsync(matchId, ct);
        return JsonSerializer.Serialize(evts, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExportEventsCsvAsync(string matchId, CancellationToken ct = default)
    {
        var evts = await events.GetByMatchIdAsync(matchId, ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("EventId,EventType,Team,Period,MatchClockSeconds,RecordingElapsedSeconds,SystemTimestamp,Source,Note,ScoreBefore,ScoreAfter");
        foreach (var e in evts)
            sb.AppendLine($"{e.EventId},{e.EventType},{e.Team},{e.Period},{e.MatchClockSeconds},{e.RecordingElapsedSeconds},{e.SystemTimestamp:O},{e.Source},{e.Note},{e.ScoreBefore},{e.ScoreAfter}");
        return sb.ToString();
    }

    public async Task<ExportManifest> GenerateManifestAsync(string matchId, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(matchId, ct)
            ?? throw new KeyNotFoundException($"Match {matchId} not found");
        var segments = recording.GetSegments();

        var recordings = segments.Select((s, i) => new ExportRecording(
            Guid.NewGuid().ToString(),
            "primary-camera",
            "Primary",
            Path.GetFileName(s.FilePath),
            s.StartTimestamp.ToString("O"),
            s.DurationSeconds ?? 0,
            "")).ToList();

        return new ExportManifest(
            FormatName, FormatVersion,
            new ExportApplication("AMHARC Match Capture", AppVersion),
            new ExportMatch(match.MatchId, match.Sport.ToString(), match.Competition,
                match.Season, match.Round, match.Date.ToString("O"),
                match.Venue, match.HomeTeam, match.AwayTeam, match.PeriodStructure.ToString()),
            recordings,
            $"{matchId}_events.json",
            $"{matchId}_score.json",
            $"{matchId}_technical.log");
    }

    public async Task WriteManifestAsync(string matchId, string outputPath, CancellationToken ct = default)
    {
        var manifest = await GenerateManifestAsync(matchId, ct);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, ct);
        logger.LogInformation("Manifest written to {Path}", outputPath);
    }

    public async Task WriteTechnicalLogAsync(string matchId, string outputPath, CancellationToken ct = default)
    {
        var evts = await events.GetByMatchIdAsync(matchId, ct);
        var match = await matches.GetByIdAsync(matchId, ct);
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"AMHARC Match Capture — Technical Log");
        lines.AppendLine($"Match: {match?.HomeTeam} v {match?.AwayTeam} ({match?.Date})");
        lines.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        lines.AppendLine(new string('-', 60));
        foreach (var e in evts)
            lines.AppendLine($"[{e.SystemTimestamp:O}] {e.EventType} | Period={e.Period} | Match={e.MatchClockSeconds}s | Rec={e.RecordingElapsedSeconds}s | Team={e.Team} | Score: {e.ScoreBefore} → {e.ScoreAfter}");
        await File.WriteAllTextAsync(outputPath, lines.ToString(), ct);
    }
}
