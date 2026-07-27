using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public enum OverlayOutputMode { Clean, Programme, OverlayOnly, OperatorPreview }

/// <summary>
/// Compatibility façade for overlay state. New broadcast work should target IBroadcastService;
/// this interface remains while existing API/UI routes are migrated incrementally.
/// </summary>
public interface IOverlayService
{
    OverlayState State { get; }
    void ShowScoreboard();
    void HideScoreboard();
    void ShowGraphic(string graphicType, int? durationMs = null);
    void HideGraphic();
    void SetOutputMode(OverlayOutputMode mode);
    void SetTemplate(string templateId);
    void UpdateScore(ScoreState score);
    void UpdateClock(int matchClockSeconds, int period);
    event Action<OverlayState> StateChanged;
}
