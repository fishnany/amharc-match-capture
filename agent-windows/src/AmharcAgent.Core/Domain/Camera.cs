namespace AmharcAgent.Core.Domain;

/// <summary>Role of a camera in the capture setup.</summary>
public enum CameraRole { Primary, Secondary, Overhead, Replay }

/// <summary>Camera connection lifecycle state.</summary>
public enum CameraConnectionState { Disconnected, Connecting, Connected, Reconnecting, Error }

/// <summary>Represents a configured camera (AXIS or other VAPIX-compatible).</summary>
public class Camera
{
    public string CameraId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = "AXIS";
    public string Model { get; set; } = string.Empty;

    /// <summary>Current IP address (DHCP — may change between sessions).</summary>
    public string IpAddress { get; set; } = string.Empty;

    public int RtspPort { get; set; } = 554;
    public int HttpPort { get; set; } = 80;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = "pass";

    public CameraRole Role { get; set; } = CameraRole.Primary;

    /// <summary>Live connection state — not persisted, set at runtime.</summary>
    public CameraConnectionState ConnectionState { get; set; } = CameraConnectionState.Disconnected;

    public DateTimeOffset? LastConnectedAt { get; set; }
    public string? StreamProfileName { get; set; }

    // Info populated after successful connection
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? MacAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
