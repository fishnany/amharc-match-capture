using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Models;

public record AmharcCommand(
    string CommandId,
    EventSource Source,
    string? MatchId = null,
    string? Operator = null,
    IReadOnlyDictionary<string, string?>? Parameters = null);

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
    public const string MatchClockHalfTimeStart = "match.clock.half-time.start";
    public const string MatchClockHalfTimeEnd = "match.clock.half-time.end";
    public const string MatchClockExtraTimeEnter = "match.clock.extra-time.enter";
    public const string MatchClockExtraTimeStart = "match.clock.extra-time.start";
    public const string MatchClockExtraTimeHalfTimeStart = "match.clock.extra-time.half-time.start";
    public const string MatchClockExtraTimeHalfTimeEnd = "match.clock.extra-time.half-time.end";
    public const string MatchAbandon = "match.abandon";
    public const string MatchClockFullTime = "match.clock.full-time";
    public const string MatchClockCorrect = "match.clock.correct";

    public const string EventUndo = "event.undo";
}