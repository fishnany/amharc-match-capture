using AmharcAgent.Core.Domain;
using System.Text.Json.Serialization;

namespace AmharcAgent.Core.Models;

/// <summary>Canonical scoring model used by the match, API and broadcast layers.</summary>
public enum ScoringModel
{
    GoalsPoints,
    GoalsTwoPointOnePoint
}

/// <summary>Immutable score for one team.</summary>
public sealed record TeamScoreState(int Goals, int TwoPointScores, int OnePointScores)
{
    public int Total => (Goals * 3) + (TwoPointScores * 2) + OnePointScores;

    public string Format(ScoringModel model) => model switch
    {
        ScoringModel.GoalsTwoPointOnePoint => $"{Goals}-{TwoPointScores}-{OnePointScores}",
        _ => $"{Goals}-{OnePointScores}"
    };
}

/// <summary>
/// Canonical API/broadcast score state. Flat team fields keep the wire contract simple,
/// while the sport and scoring model make the meaning explicit.
/// </summary>
public sealed record ScoreState(
    string MatchId,
    Sport Sport,
    ScoringModel ScoringModel,
    int HomeGoals,
    int HomeTwoPointScores,
    int HomePoints,
    int AwayGoals,
    int AwayTwoPointScores,
    int AwayPoints,
    DateTimeOffset UpdatedAt)
{
    [JsonIgnore]
    public TeamScoreState Home => new(HomeGoals, HomeTwoPointScores, HomePoints);
    [JsonIgnore]
    public TeamScoreState Away => new(AwayGoals, AwayTwoPointScores, AwayPoints);
    public int HomeTotal => Home.Total;
    public int AwayTotal => Away.Total;
    public string HomeDisplay => $"{Home.Format(ScoringModel)} ({Home.Total})";
    public string AwayDisplay => $"{Away.Format(ScoringModel)} ({Away.Total})";
}

/// <summary>Serializable score snapshot captured against a match event.</summary>
public sealed record ScoreSnapshot(ScoringModel ScoringModel, TeamScoreState Home, TeamScoreState Away)
{
    public string Display => $"{Home.Format(ScoringModel)} ({Home.Total}) / {Away.Format(ScoringModel)} ({Away.Total})";
}

/// <summary>Single source of truth for sport-specific scoring behaviour.</summary>
public static class ScoringRules
{
    public static ScoringModel GetModel(Sport sport) => sport switch
    {
        Sport.GaelicFootball => ScoringModel.GoalsTwoPointOnePoint,
        Sport.Hurling or Sport.Camogie or Sport.LadiesFootball => ScoringModel.GoalsPoints,
        _ => throw new ArgumentOutOfRangeException(nameof(sport), sport, "Unsupported sport")
    };

    public static bool SupportsTwoPointScore(Sport sport) => sport == Sport.GaelicFootball;
}
