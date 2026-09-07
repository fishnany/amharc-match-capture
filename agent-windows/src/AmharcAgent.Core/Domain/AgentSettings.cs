using AmharcAgent.Core.Models;
namespace AmharcAgent.Core.Domain;

/// <summary>Persisted user configuration for the AMHARC local agent.</summary>
public class AgentSettings
{
    /// <summary>Directory where MKV segments and final MP4s are written.</summary>
    public string RecordingDirectory { get; set; } = @"C:\AmharcRecordings";

    /// <summary>
    /// Optional explicit path to the AMHARC-managed ffmpeg.exe.
    /// When left as "ffmpeg.exe", runtime resolution uses the managed AMHARC runtime
    /// rather than the Windows PATH.
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg.exe";

    /// <summary>MKV segment duration in seconds (default 5 minutes).</summary>
    public int SegmentDurationSeconds { get; set; } = 300;

    /// <summary>Default AXIS camera username (root for factory default).</summary>
    public string DefaultCameraUsername { get; set; } = "root";

    /// <summary>
    /// Default AXIS camera password. No production credential is shipped with AMHARC.
    /// Configure this during setup or through persisted machine settings.
    /// </summary>
    public string DefaultCameraPassword { get; set; } = string.Empty;

    /// <summary>
    /// Subnet prefix to scan for cameras (e.g. "192.168.1").
    /// Null = auto-detect from network interfaces.
    /// </summary>
    public string? CameraSubnet { get; set; }

    /// <summary>Automatically scan for AXIS cameras on startup.</summary>
    public bool AutoDiscoverCameras { get; set; } = true;

    public string OperatorName { get; set; } = "Operator";

    public bool StreamDeckEnabled { get; set; } = true;

    public StreamDeckConfig StreamDeck { get; set; } = new();

    public bool JoystickEnabled { get; set; } = true;

    public JoystickConfig Joystick { get; set; } = new();
}
