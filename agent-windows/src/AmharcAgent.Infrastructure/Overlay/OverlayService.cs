using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Overlay;

/// <summary>
/// Phase 1 overlay service: manages in-memory overlay state.
/// State changes are pushed to the operator UI via SignalR.
/// Graphics rendering (NDI/HDMI output) is Phase 2.
/// </summary>
public class OverlayService(ILogger<OverlayService> logger) : IOverlayService
{
    private OverlayState _state = new(null, false, OverlayOutputMode.Clean, null, false, 0, 0, 0, 0, 0, 1);

    public OverlayState State => _state;
    public event Action<OverlayState>? StateChanged;

    public void ShowScoreboard() => Update(_state with { IsVisible = true });
    public void HideScoreboard() => Update(_state with { IsVisible = false });
    public void ShowGraphic(string graphicType, int? durationMs = null)
    {
        Update(_state with { CurrentGraphic = graphicType, GraphicVisible = true });
        if (durationMs.HasValue)
            Task.Delay(durationMs.Value).ContinueWith(_ => HideGraphic());
    }
    public void HideGraphic() => Update(_state with { GraphicVisible = false, CurrentGraphic = null });
    public void SetOutputMode(OverlayOutputMode mode) => Update(_state with { OutputMode = mode });
    public void SetTemplate(string templateId) => Update(_state with { ActiveTemplateId = templateId });

    public void UpdateScore(int homeGoals, int homePoints, int awayGoals, int awayPoints) =>
        Update(_state with
        {
            HomeGoals = homeGoals, HomePoints = homePoints,
            AwayGoals = awayGoals, AwayPoints = awayPoints
        });

    public void UpdateClock(int matchClockSeconds, int period) =>
        Update(_state with { MatchClockSeconds = matchClockSeconds, CurrentPeriod = period });

    private void Update(OverlayState next)
    {
        _state = next;
        StateChanged?.Invoke(next);
        logger.LogTrace("Overlay state updated: visible={Vis} mode={Mode}", next.IsVisible, next.OutputMode);
    }
}
