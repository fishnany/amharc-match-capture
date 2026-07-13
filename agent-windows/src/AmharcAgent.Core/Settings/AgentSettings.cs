namespace AmharcAgent.Core.Settings;

/// <summary>Top-level application settings loaded from configuration.</summary>
public class AgentSettings
{
    /// <summary>Full path to the ffmpeg executable.</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Root directory for recording output files.</summary>
    public string RecordingOutputRoot { get; set; } = "recordings";

    /// <summary>Default RTMP server URL.</summary>
    public string? DefaultRtmpServerUrl { get; set; }

    /// <summary>Default camera username.</summary>
    public string DefaultCameraUsername { get; set; } = "root";

    /// <summary>Default camera password.</summary>
    public string DefaultCameraPassword { get; set; } = "pass";

    /// <summary>Segment duration in seconds for recording.</summary>
    public int SegmentDurationSeconds { get; set; } = 300;

    /// <summary>Maximum reconnect attempts for camera connections.</summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>Maximum reconnect attempts for streaming.</summary>
    public int MaxStreamReconnectAttempts { get; set; } = 3;
}
