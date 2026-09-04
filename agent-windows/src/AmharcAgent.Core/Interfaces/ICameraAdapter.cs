using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Abstracts all camera-specific communication (AXIS VAPIX, ONVIF, etc.).</summary>
public interface ICameraAdapter
{
    string CameraId { get; }
    string Manufacturer { get; }
    string? Model { get; }
    CameraConnectionState ConnectionState { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task ReconnectAsync(CancellationToken ct = default);
    Task<string> GetStreamUrlAsync(string? profileName = null, CancellationToken ct = default);
    Task<string> GetAuthenticatedStreamUrlAsync(string? profileName = null, CancellationToken ct = default);
    Task<CameraInfo> GetCameraInfoAsync(CancellationToken ct = default);
    Task<IEnumerable<StreamProfile>> GetStreamProfilesAsync(CancellationToken ct = default);

    event Action<CameraConnectionState> ConnectionStateChanged;
    event Action<CameraHealth> HealthChanged;
}

public record CameraInfo(string? Model, string? SerialNumber, string? FirmwareVersion, string? MacAddress);
public record StreamProfile(string Name, string? Resolution, double? FrameRate, string? Codec, int? BitRate);
public record CameraHealth(double? BitRate, double? FrameRate, int? DroppedFrames, DateTimeOffset Timestamp);
