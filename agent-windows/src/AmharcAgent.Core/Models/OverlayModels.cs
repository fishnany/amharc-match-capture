using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Core.Models;

public record OverlayState(
    string? ActiveTemplateId,
    bool IsVisible,
    OverlayOutputMode OutputMode,
    string? CurrentGraphic,
    bool GraphicVisible,
    ScoreState? Score,
    int MatchClockSeconds,
    int CurrentPeriod);
