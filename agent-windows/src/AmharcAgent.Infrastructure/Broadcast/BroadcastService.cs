using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Broadcast;

/// <summary>
/// Broadcast control-plane state. Phase 1.1 renderers consume this state;
/// no video composition is performed here.
/// </summary>
public sealed class BroadcastService(ILogger<BroadcastService> logger) : IBroadcastService
{
    private BroadcastState _state = new(
        MatchId: null,
        Score: null,
        MatchClockSeconds: 0,
        CurrentPeriod: 1,
        ScoreBugVisible: false,
        ActiveTemplateId: "score-bug-v1",
        OutputMode: BroadcastOutputMode.Clean,
        CurrentGraphic: null,
        GraphicVisible: false,
        Theme: BrandAssets.DefaultTheme);

    public BroadcastState State => _state;
    public event Action<BroadcastState>? StateChanged;

    public void SetMatch(string matchId) => Update(_state with { MatchId = matchId });
    public void UpdateScore(ScoreState score) => Update(_state with { MatchId = score.MatchId, Score = score });
    public void UpdateClock(int matchClockSeconds, int currentPeriod) =>
        Update(_state with { MatchClockSeconds = matchClockSeconds, CurrentPeriod = currentPeriod });
    public void ShowScoreBug() => Update(_state with { ScoreBugVisible = true });
    public void HideScoreBug() => Update(_state with { ScoreBugVisible = false });
    public void SetOutputMode(BroadcastOutputMode mode) => Update(_state with { OutputMode = mode });
    public void SetTemplate(string templateId) => Update(_state with { ActiveTemplateId = templateId });

    public void ShowGraphic(string graphicType, int? durationMs = null)
    {
        Update(_state with { CurrentGraphic = graphicType, GraphicVisible = true });
        if (durationMs is > 0)
            _ = HideGraphicAfterAsync(graphicType, durationMs.Value);
    }

    public void HideGraphic() => Update(_state with { CurrentGraphic = null, GraphicVisible = false });

    private async Task HideGraphicAfterAsync(string graphicType, int durationMs)
    {
        await Task.Delay(durationMs);
        if (_state.GraphicVisible && _state.CurrentGraphic == graphicType)
            HideGraphic();
    }

    private void Update(BroadcastState next)
    {
        _state = next;
        StateChanged?.Invoke(next);
        logger.LogTrace("Broadcast state updated: match={MatchId} scoreBug={ScoreBug} output={Output}",
            next.MatchId, next.ScoreBugVisible, next.OutputMode);
    }
}
