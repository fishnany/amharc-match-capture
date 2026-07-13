using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Reads PTZ joystick axes via DirectInput and maps them to PTZ commands.</summary>
public interface IJoystickService
{
    bool IsConnected { get; }
    string? DeviceName { get; }
    JoystickConfig Config { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    void UpdateConfig(JoystickConfig config);

    event Action<JoystickAxisState> AxisChanged;
    event Action<int> ButtonPressed;
    event Action<string> Connected;
    event Action Disconnected;
}
