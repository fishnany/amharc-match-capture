using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public enum OverlayOutputMode { Clean, Programme, OverlayOnly, OperatorPreview }

/// <summary>Manages broadcast overlay state (scoreboard, graphics, output mode).</summary>
public interface IOverlayService
{
    OverlayState State { get; }
    void ShowScoreboard();
    void HideScoreboard();
    void ShowGraphic(string graphicType, int? durationMs = null);
    void HideGraphic();
    void SetOutputMode(OverlayOutputMode mode);
    void SetTemplate(string templateId);
    void UpdateScore(int homeGoals, int homePoints, int awayGoals, int awayPoints);
    void UpdateClock(int matchClockSeconds, int period);
    event Action<OverlayState> StateChanged;
}
