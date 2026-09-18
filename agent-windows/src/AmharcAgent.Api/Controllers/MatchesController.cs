using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController(
    IMatchRepository repo,
    IMatchClockService clock,
    ICanonicalClockSnapshotService canonicalClockSnapshotService,
    IBroadcastPresentationStateService broadcastPresentation,
    ILiveReadinessService liveReadiness,
    IAmharcCommandDispatcher commandDispatcher,
    IOverlayService overlay,
    ILogger<MatchesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMatches(
        CancellationToken ct) =>
        Ok(await repo.GetAllAsync(ct));

    [HttpGet("{matchId}/broadcast")]
    public async Task<IActionResult> GetBroadcastPresentation(
        string matchId,
        CancellationToken ct)
    {
        try
        {
            var state =
                await broadcastPresentation.CreateStateAsync(
                    matchId,
                    ct);

            return Ok(state);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                error = ex.Message
            });
        }
    }
    [HttpGet("{matchId}/readiness")]
    public async Task<IActionResult> GetLiveReadiness(
        string matchId,
        CancellationToken ct)
    {
        var state =
            await liveReadiness.EvaluateAsync(
                matchId,
                ct);

        var matchNotFound =
            state.Findings.Any(
                finding =>
                    finding.Code == "match.not-found");

        if (matchNotFound)
        {
            return NotFound(new
            {
                error =
                    $"Match '{matchId}' does not exist."
            });
        }

        return Ok(state);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMatch(
    [FromBody] CreateMatchRequest input,
    CancellationToken ct)
    {
        var sport = input.Sport switch
        {
            "gaelic-football" => Sport.GaelicFootball,
            "hurling" => Sport.Hurling,
            "camogie" => Sport.Camogie,
            "ladies-football" => Sport.LadiesFootball,
            _ => throw new ArgumentException(
                $"Unsupported sport '{input.Sport}'.")
        };

        var periodStructure = input.PeriodStructure switch
        {
            null or "halves" => PeriodStructure.TwoPeriods,
            "quarters" => PeriodStructure.FourQuarters,
            "custom" => throw new ArgumentException(
                "Custom period structures are not currently supported by the match domain."),
            _ => throw new ArgumentException(
                $"Unsupported period structure '{input.PeriodStructure}'.")
        };

        var now = DateTimeOffset.UtcNow;

        var match = new Match
        {
            MatchId = Guid.NewGuid().ToString(),
            Sport = sport,
            Competition = input.Competition,
            Season = input.Season,
            Round = input.Round,
            Date = input.Date,
            Venue = input.Venue,
            HomeTeam = input.HomeTeam,
            AwayTeam = input.AwayTeam,
            PeriodStructure = periodStructure,
            Status = MatchStatus.Setup,
            CurrentPeriod = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created =
            await repo.CreateAsync(
                match,
                ct);

        return CreatedAtAction(
            nameof(GetMatch),
            new
            {
                matchId = created.MatchId
            },
            created);
    }

    [HttpGet("{matchId}")]
    public async Task<IActionResult> GetMatch(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        return match is null
            ? NotFound()
            : Ok(match);
    }

    [HttpPut("{matchId}")]
    public async Task<IActionResult> UpdateMatch(
        string matchId,
        [FromBody] Match input,
        CancellationToken ct)
    {
        var existing =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (existing is null)
            return NotFound();

        input.MatchId =
            matchId;

        input.CreatedAt =
            existing.CreatedAt;

        return Ok(
            await repo.UpdateAsync(
                input,
                ct));
    }

    [HttpDelete("{matchId}")]
    public async Task<IActionResult> DeleteMatch(
        string matchId,
        CancellationToken ct)
    {
        await repo.DeleteAsync(
            matchId,
            ct);

        return NoContent();
    }

    [HttpPost("{matchId}/ready")]
    public async Task<IActionResult> MarkReady(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        if (match.Status != MatchStatus.Setup)
        {
            return Conflict(
                new
                {
                    message =
                        $"Match {matchId} cannot be marked ready from state {match.Status}."
                });
        }

        match.Status =
            MatchStatus.Ready;

        match.UpdatedAt =
            DateTimeOffset.UtcNow;

        var updated =
            await repo.UpdateAsync(
                match,
                ct);

        logger.LogInformation(
            "Match {Id} marked ready",
            matchId);

        return Ok(updated);
    }

    [HttpPost("{matchId}/start")]
    public async Task<IActionResult> StartMatch(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        if (match.Status != MatchStatus.Ready)
        {
            return Conflict(
                new
                {
                    message =
                        $"Match {matchId} cannot be started from state {match.Status}."
                });
        }

        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockStart,
                EventSource.Api,
                MatchId: matchId),
            ct);

        match =
            await repo.GetByIdAsync(
                matchId,
                ct)
            ?? throw new InvalidOperationException(
                $"Match {matchId} disappeared after start.");

        overlay.UpdateScore(
            match.HomeGoals,
            match.HomePoints,
            match.AwayGoals,
            match.AwayPoints);

        logger.LogInformation(
            "Match {Id} started",
            matchId);

        return Ok(match);
    }

    [HttpPost("{matchId}/stop")]
    public async Task<IActionResult> StopMatch(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockFullTime,
                EventSource.Api,
                MatchId: matchId),
            ct);

        match =
            await repo.GetByIdAsync(
                matchId,
                ct)
            ?? throw new InvalidOperationException(
                $"Match {matchId} disappeared after completion.");

        logger.LogInformation(
            "Match {Id} completed",
            matchId);

        return Ok(match);
    }

    [HttpPost("{matchId}/abandon")]
    public async Task<IActionResult> AbandonMatch(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchAbandon,
                EventSource.Api,
                MatchId: matchId),
            ct);

        match =
            await repo.GetByIdAsync(
                matchId,
                ct)
            ?? throw new InvalidOperationException(
                $"Match {matchId} disappeared after abandonment.");

        logger.LogInformation(
            "Match {Id} abandoned",
            matchId);

        return Ok(match);
    }


    // â”€â”€ Clock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("{matchId}/clock")]
    public IActionResult GetClock(
        string matchId) =>
        Ok(clock.State);

    [HttpGet("{matchId}/clock/snapshot")]
    public IActionResult GetClockSnapshot(
        string matchId) =>
        Ok(canonicalClockSnapshotService.CreateSnapshot(matchId));

    [HttpPost("{matchId}/clock/start")]
    public async Task<IActionResult> StartClock(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockStart,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/pause")]
    public async Task<IActionResult> PauseClock(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockPause,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/resume")]
    public async Task<IActionResult> ResumeClock(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockResume,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/half-time/start")]
    public async Task<IActionResult> StartHalfTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockHalfTimeStart,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/half-time/end")]
    public async Task<IActionResult> EndHalfTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockHalfTimeEnd,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/extra-time/enter")]
    public async Task<IActionResult> EnterExtraTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockExtraTimeEnter,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/extra-time/start")]
    public async Task<IActionResult> StartExtraTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockExtraTimeStart,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/extra-time/half-time/start")]
    public async Task<IActionResult> StartExtraTimeHalfTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockExtraTimeHalfTimeStart,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }

    [HttpPost("{matchId}/clock/extra-time/half-time/end")]
    public async Task<IActionResult> EndExtraTimeHalfTime(
        string matchId,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockExtraTimeHalfTimeEnd,
                EventSource.Api,
                MatchId: matchId),
            ct);

        return Ok(clock.State);
    }



    [HttpPost("{matchId}/clock/correct")]
    public async Task<IActionResult> CorrectClock(
        string matchId,
        [FromBody] ClockCorrectRequest req,
        CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockCorrect,
                EventSource.Api,
                MatchId: matchId,
                Parameters:
                    new Dictionary<string, string?>
                    {
                        ["matchClockSeconds"] =
                            req.MatchClockSeconds.ToString(),

                        ["reason"] =
                            req.Reason
                    }),
            ct);

        return Ok(clock.State);
    }

    // â”€â”€ Score â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("{matchId}/score")]
    public async Task<IActionResult> GetScore(
        string matchId,
        CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);

        if (match is null)
            return NotFound();

        return Ok(CreateScoreState(match));
    }

    [HttpPost("{matchId}/score")]
    public async Task<IActionResult> UpdateScore(
        string matchId,
        [FromBody] ScoreUpdateRequest req,
        CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);

        if (match is null)
            return NotFound();

        var team = req.Team.Trim().ToLowerInvariant();
        var scoreType = req.ScoreType.Trim().ToLowerInvariant();

        if (team is not ("home" or "away"))
        {
            return BadRequest(new { error = "Team must be 'home' or 'away'." });
        }

        if (scoreType == "one-point")
            scoreType = "point";

        if (scoreType is not ("goal" or "point" or "two-point"))
        {
            return BadRequest(new { error = "ScoreType must be 'goal', 'point', 'one-point' or 'two-point'." });
        }

        if (scoreType == "two-point" && !ScoringRules.SupportsTwoPointScore(match.Sport))
        {
            return BadRequest(new
            {
                error = $"Two-point scores are not valid for {match.Sport}."
            });
        }

        var commandId = (team, scoreType) switch
        {
            ("home", "goal") => AmharcCommandIds.ScoreHomeGoal,
            ("home", "two-point") => AmharcCommandIds.ScoreHomeTwoPoint,
            ("home", "point") => AmharcCommandIds.ScoreHomePoint,
            ("away", "goal") => AmharcCommandIds.ScoreAwayGoal,
            ("away", "two-point") => AmharcCommandIds.ScoreAwayTwoPoint,
            ("away", "point") => AmharcCommandIds.ScoreAwayPoint,
            _ => throw new InvalidOperationException("Unsupported score command.")
        };

        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                commandId,
                EventSource.Api,
                MatchId: matchId),
            ct);

        var updated = await repo.GetByIdAsync(matchId, ct);

        if (updated is null)
            return NotFound();

        return Ok(CreateScoreState(updated));
    }

    private static ScoreState CreateScoreState(Match match) => new(
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

}

public sealed record CreateMatchRequest(
    string Sport,
    string Competition,
    string Season,
    string? Round,
    DateOnly Date,
    string? Venue,
    string HomeTeam,
    string? HomeTeamShort,
    string? HomeTeamColour,
    string AwayTeam,
    string? AwayTeamShort,
    string? AwayTeamColour,
    string? PeriodStructure,
    string? CameraId);

public record ClockCorrectRequest(
    int MatchClockSeconds,
    string? Reason);

public record ScoreUpdateRequest(
    string ScoreType,
    string Team);
