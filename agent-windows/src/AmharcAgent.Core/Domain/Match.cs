using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Domain;

/// <summary>Sport type for a match.</summary>
public enum Sport { GaelicFootball, Hurling, Camogie, LadiesFootball }

/// <summary>Match lifecycle status.</summary>
public enum MatchStatus { Setup, Ready, Active, Paused, HalfTime, ExtraTimeInterval, Complete, Abandoned }

/// <summary>Period structure of the match.</summary>
public enum PeriodStructure { TwoPeriods, FourQuarters, ExtraTime }

/// <summary>Represents a single Gaelic games match.</summary>
public class Match
{
    /// <summary>Unique identifier (GUID string).</summary>
    public string MatchId { get; set; } = Guid.NewGuid().ToString();

    public Sport Sport { get; set; } = Sport.GaelicFootball;
    public string Competition { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string? Round { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string? Venue { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public MatchStatus Status { get; set; } = MatchStatus.Setup;
    public PeriodStructure PeriodStructure { get; set; } = PeriodStructure.TwoPeriods;
    public int CurrentPeriod { get; set; } = 0;

    // Score components. HomePoints/AwayPoints represent one-point scores.
    // Two-point scores apply only to men's Gaelic football.
    public int HomeGoals { get; set; } = 0;
    public int HomeTwoPointScores { get; set; } = 0;
    public int HomePoints { get; set; } = 0;
    public int AwayGoals { get; set; } = 0;
    public int AwayTwoPointScores { get; set; } = 0;
    public int AwayPoints { get; set; } = 0;

    public ScoringModel ScoringModel => ScoringRules.GetModel(Sport);

    /// <summary>Home total score in points using the selected sport's scoring model.</summary>
    public int HomeTotal => (HomeGoals * 3) + (HomeTwoPointScores * 2) + HomePoints;
    /// <summary>Away total score in points using the selected sport's scoring model.</summary>
    public int AwayTotal => (AwayGoals * 3) + (AwayTwoPointScores * 2) + AwayPoints;

    public string HomeScoreDisplay => new TeamScoreState(HomeGoals, HomeTwoPointScores, HomePoints).Format(ScoringModel);
    public string AwayScoreDisplay => new TeamScoreState(AwayGoals, AwayTwoPointScores, AwayPoints).Format(ScoringModel);

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
