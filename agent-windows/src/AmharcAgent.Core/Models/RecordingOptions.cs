namespace AmharcAgent.Core.Models;

public record RecordingOptions(
    string MatchId,
    string CameraId,
    string RtspUrl,
    string OutputDirectory,
    int SegmentDurationSeconds = 300,
    bool IncludeAudio = true);

public record RecordingSegmentInfo(
    int SegmentNumber,
    string FilePath,
    DateTimeOffset StartTimestamp,
    DateTimeOffset? EndTimestamp,
    bool IsComplete,
    double? DurationSeconds,
    long? FileSizeBytes);
