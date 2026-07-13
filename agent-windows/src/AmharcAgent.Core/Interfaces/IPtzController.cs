using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public enum PtzDirection { Left, Right, Up, Down }
public enum ZoomDirection { In, Out }

/// <summary>Pan-tilt-zoom control abstraction for any PTZ camera.</summary>
public interface IPtzController
{
    Task PanAsync(PtzDirection direction, double speed, CancellationToken ct = default);
    Task TiltAsync(PtzDirection direction, double speed, CancellationToken ct = default);
    Task ZoomAsync(ZoomDirection direction, double speed, CancellationToken ct = default);
    Task MoveAbsoluteAsync(double pan, double tilt, double zoom, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task GoHomeAsync(CancellationToken ct = default);
    Task RecallPresetAsync(string presetId, CancellationToken ct = default);
    Task SavePresetAsync(string presetId, string name, CancellationToken ct = default);
    Task EmergencyWideAsync(CancellationToken ct = default);
    Task<IEnumerable<PtzPreset>> GetPresetsAsync(CancellationToken ct = default);
}

public record PtzPreset(string PresetId, string Name, bool IsHome, string? Description);
