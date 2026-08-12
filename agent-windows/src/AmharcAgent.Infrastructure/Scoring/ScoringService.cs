using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Scoring;

/// <summary>Applies sport-aware scoring rules. The rendering layer must never calculate scores.</summary>
public sealed class ScoringService : IScoringService
{
    public ScoreState GetState(Match match) => new(
        match.MatchId,
        match.Sport,
        ScoringRules.GetModel(match.Sport),
        match.HomeGoals,
        match.HomeTwoPointScores,
        match.HomePoints,
        match.AwayGoals,
        match.AwayTwoPointScores,
        match.AwayPoints,
        match.UpdatedAt);

    public ScoreSnapshot GetSnapshot(Match match)
    {
        var state = GetState(match);
        return new ScoreSnapshot(state.ScoringModel, state.Home, state.Away);
    }

    public void Apply(Match match, EventTeam team, string scoreType, int delta = 1)
    {
        if (delta == 0) return;

        var normalized = scoreType.Trim().ToLowerInvariant().Replace('_', '-');
        if (normalized is "two-point" or "two-point-score" or "2pt")
        {
            if (!ScoringRules.SupportsTwoPointScore(match.Sport))
                throw new InvalidOperationException("Two-point scores are valid only for men's Gaelic football.");
            AddTwoPoint(match, team, delta);
        }
        else if (normalized is "goal" or "penalty-goal")
        {
            AddGoal(match, team, delta);
        }
        else if (normalized is "point" or "one-point" or "one-point-score")
        {
            AddPoint(match, team, delta);
        }
        else
        {
            throw new ArgumentException($"Unsupported score type '{scoreType}'.", nameof(scoreType));
        }

        EnsureNonNegative(match);
        match.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void AddGoal(Match match, EventTeam team, int delta)
    {
        if (team == EventTeam.Home) match.HomeGoals += delta;
        else match.AwayGoals += delta;
    }

    private static void AddTwoPoint(Match match, EventTeam team, int delta)
    {
        if (team == EventTeam.Home) match.HomeTwoPointScores += delta;
        else match.AwayTwoPointScores += delta;
    }

    private static void AddPoint(Match match, EventTeam team, int delta)
    {
        if (team == EventTeam.Home) match.HomePoints += delta;
        else match.AwayPoints += delta;
    }

    private static void EnsureNonNegative(Match match)
    {
        if (match.HomeGoals < 0 || match.HomeTwoPointScores < 0 || match.HomePoints < 0 ||
            match.AwayGoals < 0 || match.AwayTwoPointScores < 0 || match.AwayPoints < 0)
            throw new InvalidOperationException("A score correction cannot make any score component negative.");
    }
}
