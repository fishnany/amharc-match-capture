using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Broadcast;

public enum BroadcastOutputMode { Clean, Programme, OverlayOnly, OperatorPreview }

public sealed record BroadcastTheme(
    string Id,
    string Black,
    string Green,
    string Lime,
    string White,
    string PrimaryLogoAsset,
    string DarkBackgroundLogoAsset);

public sealed record BroadcastState(
    string? MatchId,
    ScoreState? Score,
    int MatchClockSeconds,
    int CurrentPeriod,
    bool ScoreBugVisible,
    string? ActiveTemplateId,
    BroadcastOutputMode OutputMode,
    string? CurrentGraphic,
    bool GraphicVisible,
    BroadcastTheme Theme);
