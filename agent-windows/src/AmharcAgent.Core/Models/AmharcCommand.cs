using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Models;

public record AmharcCommand(
    string CommandId,
    EventSource Source,
    string? MatchId = null,
    string? Operator = null);

public static class AmharcCommandIds
{
    public const string ScoreHomeGoal = "score.home.goal";
    public const string ScoreHomeTwoPoint = "score.home.two-point";
    public const string ScoreHomePoint = "score.home.point";

    public const string ScoreAwayGoal = "score.away.goal";
    public const string ScoreAwayTwoPoint = "score.away.two-point";
    public const string ScoreAwayPoint = "score.away.point";

    public const string MatchClockStart = "match.clock.start";
    public const string MatchClockPause = "match.clock.pause";
    public const string MatchClockResume = "match.clock.resume";

    public const string EventUndo = "event.undo";
}