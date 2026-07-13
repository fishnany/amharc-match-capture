using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Camera;

/// <summary>
/// Implements ICameraAdapter and IPtzController for the AXIS Q6128-E via VAPIX.
/// Handles connect/disconnect lifecycle, auto-reconnect, and PTZ commands.
/// </summary>
public class AxisCameraAdapter : ICameraAdapter, IPtzController, IAsyncDisposable
{
    private readonly ILogger<AxisCameraAdapter> _logger;
    private readonly Core.Domain.Camera _config;
    private AxisVapixClient? _client;
    private CameraConnectionState _state = CameraConnectionState.Disconnected;
    private readonly Lock _lock = new();
    private CancellationTokenSource? _reconnectCts;
    private const int MaxReconnectAttempts = 5;

    public AxisCameraAdapter(Core.Domain.Camera config, ILogger<AxisCameraAdapter> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string CameraId => _config.CameraId;
    public string Manufacturer => _config.Manufacturer;
    public string? Model => _config.Model;
    public CameraConnectionState ConnectionState => _state;

    public event Action<CameraConnectionState>? ConnectionStateChanged;
    public event Action<CameraHealth>? HealthChanged;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        SetState(CameraConnectionState.Connecting);
        _client = new AxisVapixClient(_config.IpAddress, _config.Username, _config.Password,
            _logger as ILogger<AxisVapixClient> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AxisVapixClient>.Instance);
        try
        {
            var info = await _client.GetDeviceInfoAsync(ct);
            _config.SerialNumber = info.SerialNumber;
            _config.FirmwareVersion = info.FirmwareVersion;
            _config.MacAddress = info.MacAddress;
            _config.LastConnectedAt = DateTimeOffset.UtcNow;
            _logger.LogInformation("Connected to AXIS {Model} at {Ip}", info.Model, _config.IpAddress);
            SetState(CameraConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to camera at {Ip}", _config.IpAddress);
            SetState(CameraConnectionState.Error);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _reconnectCts?.Cancel();
        _client?.Dispose();
        _client = null;
        SetState(CameraConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public async Task ReconnectAsync(CancellationToken ct = default)
    {
        _reconnectCts?.Cancel();
        _reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _reconnectCts.Token;

        for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
        {
            if (token.IsCancellationRequested) break;
            SetState(CameraConnectionState.Reconnecting);
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s, 8s, 16s
            _logger.LogWarning("Reconnect attempt {Attempt}/{Max} in {Delay}s", attempt, MaxReconnectAttempts, delay.TotalSeconds);
            await Task.Delay(delay, token);
            try
            {
                await ConnectAsync(token);
                return;
            }
            catch { /* try again */ }
        }
        SetState(CameraConnectionState.Error);
        _logger.LogError("All reconnect attempts failed for camera {Id}", CameraId);
    }

    public Task<string> GetStreamUrlAsync(string? profileName = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return Task.FromResult(_client!.GetRtspUrl(profileName));
    }

    public Task<CameraInfo> GetCameraInfoAsync(CancellationToken ct = default) =>
        Task.FromResult(new CameraInfo(_config.Model, _config.SerialNumber,
            _config.FirmwareVersion, _config.MacAddress));

    public Task<IEnumerable<StreamProfile>> GetStreamProfilesAsync(CancellationToken ct = default) =>
        Task.FromResult<IEnumerable<StreamProfile>>([
            new StreamProfile("Quality", "3840x2160", 25, "H.264", 8000),
            new StreamProfile("Medium", "1920x1080", 25, "H.264", 4000),
            new StreamProfile("Mobile", "1280x720", 25, "H.264", 2000)
        ]);

    // ── PTZ ──────────────────────────────────────────────────────────────────

    public async Task PanAsync(PtzDirection direction, double speed, CancellationToken ct = default)
    {
        EnsureConnected();
        var s = (int)Math.Clamp(speed * 100, -100, 100);
        int pan = direction switch { PtzDirection.Left => -s, PtzDirection.Right => s, _ => 0 };
        await _client!.PtzContinuousMoveAsync(pan, 0, ct);
    }

    public async Task TiltAsync(PtzDirection direction, double speed, CancellationToken ct = default)
    {
        EnsureConnected();
        var s = (int)Math.Clamp(speed * 100, -100, 100);
        int tilt = direction switch { PtzDirection.Up => s, PtzDirection.Down => -s, _ => 0 };
        await _client!.PtzContinuousMoveAsync(0, tilt, ct);
    }

    public async Task ZoomAsync(ZoomDirection direction, double speed, CancellationToken ct = default)
    {
        EnsureConnected();
        var s = (int)Math.Clamp(speed * 100, -100, 100);
        await _client!.PtzContinuousZoomAsync(direction == ZoomDirection.In ? s : -s, ct);
    }

    public async Task MoveAbsoluteAsync(double pan, double tilt, double zoom, CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.PtzMoveAbsoluteAsync(pan, tilt, zoom, ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.PtzStopAsync(ct);
    }

    public async Task GoHomeAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await _client!.PtzGoHomeAsync(ct);
    }

    public async Task RecallPresetAsync(string presetId, CancellationToken ct = default)
    {
        EnsureConnected();
        if (int.TryParse(presetId, out var num))
            await _client!.RecallPresetAsync(num, ct);
    }

    public async Task SavePresetAsync(string presetId, string name, CancellationToken ct = default)
    {
        EnsureConnected();
        if (int.TryParse(presetId, out var num))
            await _client!.SavePresetAsync(num, name, ct);
    }

    public async Task EmergencyWideAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        // Zoom all the way out to widest angle
        await _client!.PtzMoveAbsoluteAsync(0, 0, 1, ct);
    }

    public async Task<IEnumerable<PtzPreset>> GetPresetsAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        var vapixPresets = await _client!.GetPresetsAsync(ct);
        return vapixPresets.Select(p => new PtzPreset(p.PresetId, p.Name, p.IsHome, null));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void SetState(CameraConnectionState state)
    {
        lock (_lock) { _state = state; }
        ConnectionStateChanged?.Invoke(state);
    }

    private void EnsureConnected()
    {
        if (_state != CameraConnectionState.Connected || _client is null)
            throw new InvalidOperationException($"Camera {CameraId} is not connected (state: {_state}).");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
