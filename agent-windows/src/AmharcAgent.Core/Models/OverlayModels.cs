using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Core.Models;

public record OverlayState(
    string? ActiveTemplateId,
    bool IsVisible,
    OverlayOutputMode OutputMode,
    string? CurrentGraphic,
    bool GraphicVisible,
    int HomeGoals,
    int HomePoints,
    int AwayGoals,
    int AwayPoints,
    int MatchClockSeconds,
    int CurrentPeriod);
