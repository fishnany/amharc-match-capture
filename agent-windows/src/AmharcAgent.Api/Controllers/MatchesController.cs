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
    IAmharcCommandDispatcher commandDispatcher,
    IOverlayService overlay,
    ILogger<MatchesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMatches(
        CancellationToken ct) =>
        Ok(await repo.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateMatch(
        [FromBody] Match input,
        CancellationToken ct)
    {
        input.MatchId =
            Guid.NewGuid().ToString();

        input.CreatedAt =
            input.UpdatedAt =
                DateTimeOffset.UtcNow;

        var created =
            await repo.CreateAsync(
                input,
                ct);

        return CreatedAtAction(
            nameof(GetMatch),
            new
            {
                matchId =
                    created.MatchId
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


    // ── Clock ─────────────────────────────────────────────────────────────────

    [HttpGet("{matchId}/clock")]
    public IActionResult GetClock(
        string matchId) =>
        Ok(clock.State);

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

    // ── Score ─────────────────────────────────────────────────────────────────

    [HttpGet("{matchId}/score")]
    public async Task<IActionResult> GetScore(
        string matchId,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        return Ok(new
        {
            homeGoals =
                match.HomeGoals,

            homePoints =
                match.HomePoints,

            homeTotal =
                match.HomeTotal,

            awayGoals =
                match.AwayGoals,

            awayPoints =
                match.AwayPoints,

            awayTotal =
                match.AwayTotal
        });
    }

    [HttpPost("{matchId}/score")]
    public async Task<IActionResult> UpdateScore(
        string matchId,
        [FromBody] ScoreUpdateRequest req,
        CancellationToken ct)
    {
        var match =
            await repo.GetByIdAsync(
                matchId,
                ct);

        if (match is null)
            return NotFound();

        if (req.Team == "home")
        {
            if (req.ScoreType == "goal")
                match.HomeGoals++;
            else if (req.ScoreType == "point")
                match.HomePoints++;
        }
        else if (req.Team == "away")
        {
            if (req.ScoreType == "goal")
                match.AwayGoals++;
            else if (req.ScoreType == "point")
                match.AwayPoints++;
        }

        await repo.UpdateAsync(
            match,
            ct);

        overlay.UpdateScore(
            match.HomeGoals,
            match.HomePoints,
            match.AwayGoals,
            match.AwayPoints);

        return Ok(new
        {
            homeGoals =
                match.HomeGoals,

            homePoints =
                match.HomePoints,

            homeTotal =
                match.HomeTotal,

            awayGoals =
                match.AwayGoals,

            awayPoints =
                match.AwayPoints,

            awayTotal =
                match.AwayTotal
        });
    }
}

public record ClockCorrectRequest(
    int MatchClockSeconds,
    string? Reason);

public record ScoreUpdateRequest(
    string ScoreType,
    string Team);
