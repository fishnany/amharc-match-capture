using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Broadcast;

public sealed record EventBannerViewModel(
    string MatchId,
    Sport Sport,
    string EventType,
    string TeamName,
    string TeamAbbreviation,
    int? PlayerNumber,
    string? PlayerName,
    string LogoAssetPath,
    BroadcastTheme Theme);
