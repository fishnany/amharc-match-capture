using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Health;

public class HealthMonitoringService : IHealthMonitoringService, IDisposable
{
    private readonly ICameraAdapter _camera;
    private readonly IRecordingService _recording;
    private readonly IStreamingService _streaming;
    private readonly IStorageMonitorService _storage;
    private readonly IStreamDeckService _streamDeck;
    private readonly IJoystickService _joystick;
    private readonly IOverlayService _overlay;
    private readonly ILogger<HealthMonitoringService> _logger;
    private readonly System.Threading.Timer _timer;

    public event Action<string, string>? Warning;
    public event Action<string, string>? ErrorOccurred;

    public HealthMonitoringService(
        ICameraAdapter camera, IRecordingService recording,
        IStreamingService streaming, IStorageMonitorService storage,
        IStreamDeckService streamDeck, IJoystickService joystick,
        IOverlayService overlay, ILogger<HealthMonitoringService> logger)
    {
        _camera = camera; _recording = recording; _streaming = streaming;
        _storage = storage; _streamDeck = streamDeck; _joystick = joystick;
        _overlay = overlay; _logger = logger;

        _timer = new System.Threading.Timer(_ => CheckAll(), null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public SystemHealth Health => GetHealth();

    public SystemHealth GetHealth()
    {
        var now = DateTimeOffset.UtcNow;
        var camera = BuildCamera(now);
        var recording = BuildRecording(now);
        var streaming = BuildStreaming(now);
        var storage = BuildStorage(now);
        var streamDeck = BuildStreamDeck(now);
        var joystick = BuildJoystick(now);
        var overlay = new ComponentHealth("Overlay", ComponentHealthState.Healthy, null, now);
        var audio = new ComponentHealth("Audio", ComponentHealthState.Unknown, "Not monitored in Phase 1", now);
        var api = new ComponentHealth("LocalApi", ComponentHealthState.Healthy, null, now);

        var overall = new[] { camera, recording, streaming, storage, streamDeck, joystick }
            .Select(c => c.State)
            .OrderByDescending(s => s)
            .First();

        return new SystemHealth(camera, recording, streaming, storage, streamDeck, joystick,
            overlay, audio, api, overall);
    }

    private ComponentHealth BuildCamera(DateTimeOffset now) => _camera.ConnectionState switch
    {
        CameraConnectionState.Connected => new("Camera", ComponentHealthState.Healthy, null, now),
        CameraConnectionState.Reconnecting => new("Camera", ComponentHealthState.Degraded, "Reconnecting", now),
        CameraConnectionState.Error => new("Camera", ComponentHealthState.Critical, "Connection error", now),
        _ => new("Camera", ComponentHealthState.Unknown, "Disconnected", now)
    };

    private ComponentHealth BuildRecording(DateTimeOffset now) => _recording.State switch
    {
        RecordingState.Recording => new("Recording", ComponentHealthState.Healthy, null, now),
        RecordingState.Error => new("Recording", ComponentHealthState.Critical, "Recording error", now),
        RecordingState.Recovering => new("Recording", ComponentHealthState.Degraded, "Recovering", now),
        _ => new("Recording", ComponentHealthState.Healthy, $"Idle ({_recording.State})", now)
    };

    private ComponentHealth BuildStreaming(DateTimeOffset now) => _streaming.State switch
    {
        StreamingState.Streaming => new("Streaming", ComponentHealthState.Healthy, null, now),
        StreamingState.Error => new("Streaming", ComponentHealthState.Critical, "Streaming error", now),
        StreamingState.Reconnecting => new("Streaming", ComponentHealthState.Degraded, "Reconnecting", now),
        _ => new("Streaming", ComponentHealthState.Healthy, $"Idle ({_streaming.State})", now)
    };

    private ComponentHealth BuildStorage(DateTimeOffset now) => _storage.Status.WarningLevel switch
    {
        StorageWarningLevel.Ok => new("Storage", ComponentHealthState.Healthy, null, now),
        StorageWarningLevel.Warning => new("Storage", ComponentHealthState.Degraded,
            $"{_storage.Status.AvailableMinutes:F0} min remaining", now),
        _ => new("Storage", ComponentHealthState.Critical,
            $"Only {_storage.Status.AvailableMinutes:F0} min remaining", now)
    };

    private ComponentHealth BuildStreamDeck(DateTimeOffset now) =>
        new("StreamDeck",
            _streamDeck.IsConnected ? ComponentHealthState.Healthy : ComponentHealthState.Unknown,
            _streamDeck.IsConnected ? null : "Not connected", now);

    private ComponentHealth BuildJoystick(DateTimeOffset now) =>
        new("Joystick",
            _joystick.IsConnected ? ComponentHealthState.Healthy : ComponentHealthState.Unknown,
            _joystick.IsConnected ? null : "Not connected", now);

    private void CheckAll()
    {
        var health = GetHealth();
        foreach (var comp in new[] { health.Camera, health.Recording, health.Streaming, health.Storage })
        {
            if (comp.State == ComponentHealthState.Critical)
                ErrorOccurred?.Invoke(comp.Component, comp.Message ?? "Critical error");
            else if (comp.State == ComponentHealthState.Degraded)
                Warning?.Invoke(comp.Component, comp.Message ?? "Degraded");
        }
    }

    public void Dispose() => _timer.Dispose();
}
