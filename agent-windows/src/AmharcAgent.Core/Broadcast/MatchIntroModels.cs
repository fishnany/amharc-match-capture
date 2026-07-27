using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Broadcast;

public sealed record MatchIntroViewModel(
    string MatchId,
    Sport Sport,
    string Competition,
    string? Round,
    string HomeTeam,
    string AwayTeam,
    string? Venue,
    DateOnly Date,
    string ThrowInLabel,
    string LogoAssetPath,
    BroadcastTheme Theme);
