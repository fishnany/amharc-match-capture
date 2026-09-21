using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Joystick;

/// <summary>
/// Bridges AXIS T8311 joystick input to the active PTZ camera.
///
/// The latest joystick position is sampled at a controlled rate instead of
/// issuing an HTTP request for every DirectInput event. Pan and tilt are sent
/// together to provide smooth diagonal and continuous camera movement.
/// </summary>
public sealed class JoystickPtzBridge : IAsyncDisposable
{
    private readonly IJoystickService _joystick;
    private readonly IPtzController _ptz;
    private readonly ILogger<JoystickPtzBridge> _logger;

    private readonly object _stateLock = new();

    private JoystickAxisState _latestAxes = new(0, 0, 0);

    private CancellationTokenSource? _cts;
    private Task? _controlLoop;

    private bool _started;

    public JoystickPtzBridge(
        IJoystickService joystick,
        IPtzController ptz,
        ILogger<JoystickPtzBridge> logger)
    {
        _joystick = joystick;
        _ptz = ptz;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
            return;

        _joystick.AxisChanged += OnAxisChanged;
        _joystick.ButtonPressed += OnButtonPressed;

        _cts = new CancellationTokenSource();

        _controlLoop = Task.Run(
            () => ControlLoopAsync(_cts.Token));

        _started = true;

        _logger.LogInformation(
            "Joystick PTZ bridge started");
    }

    private void OnAxisChanged(
        JoystickAxisState axes)
    {
        lock (_stateLock)
        {
            _latestAxes = axes;
        }
    }

    private void OnButtonPressed(int button)
    {
        _ = HandleButtonAsync(button);
    }

    private async Task ControlLoopAsync(
        CancellationToken ct)
    {
        // Approximately 12.5 PTZ updates per second.
        // This avoids flooding the camera with HTTP commands.
        var interval = TimeSpan.FromMilliseconds(
            _joystick.Config.PtzUpdateIntervalMs);

        while (!ct.IsCancellationRequested)
        {
            JoystickAxisState axes;

            lock (_stateLock)
            {
                axes = _latestAxes;
            }

            try
            {
                await SendPtzStateAsync(
                    axes,
                    ct);
            }
            catch (InvalidOperationException)
            {
                // Camera has not yet been connected.
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Joystick PTZ command failed");
            }

            await Task.Delay(
                interval,
                ct);
        }
    }

    private async Task SendPtzStateAsync(
        JoystickAxisState axes,
        CancellationToken ct)
    {
        const double threshold = 0.04;

        var pan =
            Math.Abs(axes.Pan) > threshold
                ? ApplyResponseCurve(axes.Pan)
                : 0.0;

        var tilt =
            Math.Abs(axes.Tilt) > threshold
                ? ApplyResponseCurve(axes.Tilt)
                : 0.0;

        var zoom =
            Math.Abs(axes.Zoom) > threshold
                ? ApplyResponseCurve(axes.Zoom)
                : 0.0;
        
        // Send pan and tilt as one VAPIX command.
        await _ptz.MoveContinuousAsync(
            pan,
            tilt,
            zoom,
            ct);
    }

    /// <summary>
    /// Provides finer control around centre while retaining higher speed
    /// at larger joystick deflections.
    /// </summary>
    private static double ApplyResponseCurve(
        double value)
    {
        var sign = Math.Sign(value);
        var magnitude = Math.Abs(value);

        // Gentle quadratic response.
        var curved = magnitude * magnitude;

        // Prevent valid movement from becoming excessively slow.
        var output =
            0.15 + (0.85 * curved);

        return sign * Math.Clamp(
            output,
            0.0,
            1.0);
    }

    private async Task HandleButtonAsync(
        int button)
    {
        try
        {
            _logger.LogInformation(
                "Joystick button {Button} pressed",
                button);

            switch (button)
            {
                // Confirmed AXIS T8311 mapping:
                // J1 = 0
                // J2 = 1
                // J3 = 2
                // J4 = 3
                // L  = 4
                // R  = 5

                case 0:
                    await _ptz.RecallPresetAsync("1");
                    break;

                case 1:
                    await _ptz.RecallPresetAsync("2");
                    break;

                case 2:
                    await _ptz.RecallPresetAsync("3");
                    break;

                case 3:
                    await _ptz.RecallPresetAsync("4");
                    break;

                case 4:
                    await _ptz.GoHomeAsync();
                    break;

                case 5:
                    await _ptz.EmergencyWideAsync();
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // Camera has not yet been connected.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Joystick button {Button} command failed",
                button);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            _joystick.AxisChanged -= OnAxisChanged;
            _joystick.ButtonPressed -= OnButtonPressed;

            _cts?.Cancel();

            if (_controlLoop is not null)
            {
                try
                {
                    await _controlLoop;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        _cts?.Dispose();
    }
}