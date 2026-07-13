namespace AmharcAgent.Core.Models;

public record JoystickAxisState(double Pan, double Tilt, double Zoom);

public record JoystickConfig(
    double DeadZone = 0.05,
    double PanSensitivity = 1.0,
    double TiltSensitivity = 1.0,
    double ZoomSensitivity = 1.0,
    bool InvertPan = false,
    bool InvertTilt = false,
    bool InvertZoom = false);
