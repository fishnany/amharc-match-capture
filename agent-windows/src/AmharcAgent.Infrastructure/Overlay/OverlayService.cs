using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Overlay;

/// <summary>
/// Compatibility overlay state façade. Broadcast Lite renderers will consume canonical ScoreState.
/// </summary>
public class OverlayService(ILogger<OverlayService> logger) : IOverlayService
{
    private OverlayState _state = new(null, false, OverlayOutputMode.Clean, null, false, null, 0, 1);

    public OverlayState State => _state;
    public event Action<OverlayState>? StateChanged;

    public void ShowScoreboard() => Update(_state with { IsVisible = true });
    public void HideScoreboard() => Update(_state with { IsVisible = false });

    public void ShowGraphic(string graphicType, int? durationMs = null)
    {
        Update(_state with { CurrentGraphic = graphicType, GraphicVisible = true });
        if (durationMs is > 0)
            _ = HideGraphicAfterAsync(graphicType, durationMs.Value);
    }

    public void HideGraphic() => Update(_state with { GraphicVisible = false, CurrentGraphic = null });
    public void SetOutputMode(OverlayOutputMode mode) => Update(_state with { OutputMode = mode });
    public void SetTemplate(string templateId) => Update(_state with { ActiveTemplateId = templateId });
    public void UpdateScore(ScoreState score) => Update(_state with { Score = score });
    public void UpdateClock(int matchClockSeconds, int period) =>
        Update(_state with { MatchClockSeconds = matchClockSeconds, CurrentPeriod = period });

    private async Task HideGraphicAfterAsync(string graphicType, int durationMs)
    {
        await Task.Delay(durationMs);
        if (_state.GraphicVisible && _state.CurrentGraphic == graphicType)
            HideGraphic();
    }

    private void Update(OverlayState next)
    {
        _state = next;
        StateChanged?.Invoke(next);
        logger.LogTrace("Overlay state updated: visible={Vis} mode={Mode}", next.IsVisible, next.OutputMode);
    }
}
