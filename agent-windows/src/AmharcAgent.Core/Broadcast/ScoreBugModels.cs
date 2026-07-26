using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Broadcast;

/// <summary>Resolved discipline state shown by the score bug.</summary>
public sealed record TeamDisciplineState(bool HasRedCard, bool HasActiveBlackCard);

/// <summary>Presentation model consumed by a score bug renderer.</summary>
public sealed record ScoreBugViewModel(
    string MatchId,
    Sport Sport,
    ScoringModel ScoringModel,
    string HomeTeam,
    string HomeAbbreviation,
    TeamScoreState HomeScore,
    TeamDisciplineState HomeDiscipline,
    string AwayTeam,
    string AwayAbbreviation,
    TeamScoreState AwayScore,
    TeamDisciplineState AwayDiscipline,
    int MatchClockSeconds,
    int CurrentPeriod,
    MatchStatus MatchStatus,
    bool ReplayMode,
    string LogoAssetPath,
    BroadcastTheme Theme);
