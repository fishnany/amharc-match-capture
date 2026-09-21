namespace AmharcAgent.Core.Domain;

/// <summary>State of the recording pipeline.</summary>
public enum RecordingState
{
    Idle, Starting, Recording, Rotating, Stopping, Remuxing, Complete, Error, Recovering
}

/// <summary>Represents a recording session for a match.</summary>
public class RecordingSession
{
    public string RecordingId { get; set; } = Guid.NewGuid().ToString();
    public string MatchId { get; set; } = string.Empty;
    public string CameraId { get; set; } = string.Empty;
    public RecordingState State { get; set; } = RecordingState.Idle;
    public string OutputDirectory { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }

    /// <summary>Target MKV segment duration in seconds (default 5 minutes).</summary>
    public int SegmentDurationSeconds { get; set; } = 300;

    /// <summary>Whether audio is included in the recording.</summary>
    public bool IncludeAudio { get; set; } = true;

    public int SegmentCount { get; set; }

    /// <summary>Path to final remuxed MP4 file (set after RemuxToMp4).</summary>
    public string? FinalFilePath { get; set; }

    /// <summary>SHA-256 checksum of the final MP4.</summary>
    public string? Checksum { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
