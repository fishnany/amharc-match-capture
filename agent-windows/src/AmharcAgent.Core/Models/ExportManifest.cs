namespace AmharcAgent.Core.Models;

public record ExportManifest(
    string Format,
    int FormatVersion,
    ExportApplication Application,
    ExportMatch Match,
    IReadOnlyList<ExportRecording> Recordings,
    string EventFile,
    string ScoreFile,
    string TechnicalLog);

public record ExportApplication(string Name, string Version);

public record ExportMatch(
    string MatchId,
    string Sport,
    string Competition,
    string Season,
    string? Round,
    string Date,
    string? Venue,
    string HomeTeam,
    string AwayTeam,
    string PeriodStructure);

public record ExportRecording(
    string RecordingId,
    string CameraId,
    string CameraRole,
    string File,
    string StartTimestamp,
    double DurationSeconds,
    string Checksum);
