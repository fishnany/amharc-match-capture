namespace AmharcAgent.Core.Models;

/// <summary>Recording session entry in the export manifest.</summary>
public record RecordingManifestEntry(
    string RecordingId,
    string CameraId,
    string CameraRole,
    string File,
    string StartTimestamp,
    double DurationSeconds,
    string Checksum);

/// <summary>Full export manifest describing a match capture session.</summary>
public record ExportManifest(
    string Format,
    int FormatVersion,
    ExportManifestApplication Application,
    ExportManifestMatch Match,
    IReadOnlyList<RecordingManifestEntry> Recordings,
    string EventFile,
    string ScoreFile,
    string TechnicalLog);

/// <summary>Application info section of the manifest.</summary>
public record ExportManifestApplication(string Name, string Version);

/// <summary>Match info section of the manifest.</summary>
public record ExportManifestMatch(
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
