using AmharcAgent.Core.Models;
namespace AmharcAgent.Core.Domain;

/// <summary>Persisted user configuration for the AMHARC local agent.</summary>
public class AgentSettings
{
    /// <summary>Directory where MKV segments and final MP4s are written.</summary>
    public string RecordingDirectory { get; set; } = @"C:\AmharcRecordings";

    /// <summary>Path to ffmpeg.exe (bundled in install directory by default).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg.exe";

    /// <summary>MKV segment duration in seconds (default 5 minutes).</summary>
    public int SegmentDurationSeconds { get; set; } = 300;

    /// <summary>Default AXIS camera username (root for factory default).</summary>
    public string DefaultCameraUsername { get; set; } = "root";

    /// <summary>Default AXIS camera password (pass for factory default).</summary>
    public string DefaultCameraPassword { get; set; } = "pass";

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
