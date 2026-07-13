using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;

namespace AmharcAgent.Infrastructure.Joystick;

/// <summary>
/// Polls a DirectInput joystick and maps axes to PTZ pan/tilt/zoom values.
/// Axis values are normalised to -1.0..1.0 with configurable dead zone and sensitivity.
/// </summary>
public class JoystickService : IJoystickService, IAsyncDisposable
{
    private readonly ILogger<JoystickService> _logger;
    private DirectInput? _directInput;
    private SharpDX.DirectInput.Joystick? _joystick;
    private CancellationTokenSource? _cts;
    private JoystickConfig _config;
    private JoystickAxisState _lastAxes = new(0, 0, 0);
    private readonly int[] _lastButtons = new int[32];

    public bool IsConnected { get; private set; }
    public string? DeviceName { get; private set; }
    public JoystickConfig Config => _config;

    public event Action<JoystickAxisState>? AxisChanged;
    public event Action<int>? ButtonPressed;
    public event Action<string>? Connected;
    public event Action? Disconnected;

    public JoystickService(ILogger<JoystickService> logger, JoystickConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new JoystickConfig();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => PollLoop(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        CloseDevice();
        await Task.CompletedTask;
    }

    public void UpdateConfig(JoystickConfig config) => _config = config;

    // ── internals ────────────────────────────────────────────────────────────

    private async Task PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!IsConnected)
            {
                await TryConnectAsync(ct);
                if (!IsConnected)
                {
                    await Task.Delay(3000, ct);
                    continue;
                }
            }

            try
            {
                _joystick!.Poll();
                var state = _joystick.GetCurrentState();

                // Axes: X=pan, Y=tilt, Z or RotationZ=zoom
                // DirectInput axis range: 0–65535, centre = 32767
                var pan = Normalise(state.X, _config.PanSensitivity, _config.InvertPan);
                var tilt = Normalise(state.Y, _config.TiltSensitivity, _config.InvertTilt);
                var zoom = Normalise(state.Z, _config.ZoomSensitivity, _config.InvertZoom);

                var axes = new JoystickAxisState(pan, tilt, zoom);
                if (Math.Abs(axes.Pan - _lastAxes.Pan) > 0.01
                    || Math.Abs(axes.Tilt - _lastAxes.Tilt) > 0.01
                    || Math.Abs(axes.Zoom - _lastAxes.Zoom) > 0.01)
                {
                    _lastAxes = axes;
                    AxisChanged?.Invoke(axes);
                }

                // Button edge detection
                var buttons = state.Buttons;
                for (var i = 0; i < Math.Min(buttons.Length, _lastButtons.Length); i++)
                {
                    var pressed = buttons[i] ? 1 : 0;
                    if (pressed == 1 && _lastButtons[i] == 0)
                        ButtonPressed?.Invoke(i);
                    _lastButtons[i] = pressed;
                }

                await Task.Delay(50, ct); // 20Hz polling
            }
            catch (SharpDX.SharpDXException)
            {
                _logger.LogWarning("Joystick disconnected");
                CloseDevice();
            }
        }
    }

    private async Task TryConnectAsync(CancellationToken ct)
    {
        try
        {
            _directInput = new DirectInput();
            var devices = _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices);
            if (devices.Count == 0) return;

            var deviceInstance = devices[0];
            _joystick = new SharpDX.DirectInput.Joystick(_directInput, deviceInstance.InstanceGuid);
            _joystick.Properties.BufferSize = 128;
            _joystick.Acquire();

            DeviceName = deviceInstance.InstanceName;
            IsConnected = true;
            _logger.LogInformation("Joystick connected: {Name}", DeviceName);
            Connected?.Invoke(DeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Joystick not found: {Msg}", ex.Message);
            CloseDevice();
        }
        await Task.CompletedTask;
    }

    private double Normalise(int rawValue, double sensitivity, bool invert)
    {
        // Map 0–65535 to -1.0..1.0
        var normalised = (rawValue - 32767.0) / 32767.0;
        normalised = Math.Clamp(normalised, -1.0, 1.0);

        // Apply dead zone
        if (Math.Abs(normalised) < _config.DeadZone) return 0.0;

        // Scale out of dead zone
        var sign = normalised > 0 ? 1.0 : -1.0;
        normalised = sign * (Math.Abs(normalised) - _config.DeadZone) / (1.0 - _config.DeadZone);

        return Math.Clamp(normalised * sensitivity * (invert ? -1 : 1), -1.0, 1.0);
    }

    private void CloseDevice()
    {
        try { _joystick?.Unacquire(); _joystick?.Dispose(); } catch { }
        try { _directInput?.Dispose(); } catch { }
        _joystick = null;
        _directInput = null;
        if (IsConnected) { IsConnected = false; DeviceName = null; Disconnected?.Invoke(); }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
