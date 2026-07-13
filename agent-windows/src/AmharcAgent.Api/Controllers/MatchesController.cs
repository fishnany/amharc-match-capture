using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController(
    IMatchRepository repo,
    IMatchClockService clock,
    IOverlayService overlay,
    ILogger<MatchesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMatches(CancellationToken ct) =>
        Ok(await repo.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateMatch([FromBody] Match input, CancellationToken ct)
    {
        input.MatchId = Guid.NewGuid().ToString();
        input.CreatedAt = input.UpdatedAt = DateTimeOffset.UtcNow;
        var created = await repo.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetMatch), new { matchId = created.MatchId }, created);
    }

    [HttpGet("{matchId}")]
    public async Task<IActionResult> GetMatch(string matchId, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPut("{matchId}")]
    public async Task<IActionResult> UpdateMatch(string matchId, [FromBody] Match input, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(matchId, ct);
        if (existing is null) return NotFound();
        input.MatchId = matchId;
        input.CreatedAt = existing.CreatedAt;
        return Ok(await repo.UpdateAsync(input, ct));
    }

    [HttpDelete("{matchId}")]
    public async Task<IActionResult> DeleteMatch(string matchId, CancellationToken ct)
    {
        await repo.DeleteAsync(matchId, ct);
        return NoContent();
    }

    [HttpPost("{matchId}/start")]
    public async Task<IActionResult> StartMatch(string matchId, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        match.Status = MatchStatus.Active;
        match.CurrentPeriod = 1;
        await repo.UpdateAsync(match, ct);
        clock.Start();
        overlay.UpdateScore(match.HomeGoals, match.HomePoints, match.AwayGoals, match.AwayPoints);
        logger.LogInformation("Match {Id} started", matchId);
        return Ok(match);
    }

    [HttpPost("{matchId}/stop")]
    public async Task<IActionResult> StopMatch(string matchId, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        match.Status = MatchStatus.Complete;
        await repo.UpdateAsync(match, ct);
        clock.MarkFullTime();
        return Ok(match);
    }

    // ── Clock ─────────────────────────────────────────────────────────────────

    [HttpGet("{matchId}/clock")]
    public IActionResult GetClock(string matchId) => Ok(clock.State);

    [HttpPost("{matchId}/clock/start")]
    public IActionResult StartClock(string matchId) { clock.Start(); return Ok(clock.State); }

    [HttpPost("{matchId}/clock/pause")]
    public IActionResult PauseClock(string matchId) { clock.Pause(); return Ok(clock.State); }

    [HttpPost("{matchId}/clock/resume")]
    public IActionResult ResumeClock(string matchId) { clock.Resume(); return Ok(clock.State); }

    [HttpPost("{matchId}/clock/correct")]
    public IActionResult CorrectClock(string matchId, [FromBody] ClockCorrectRequest req)
    {
        clock.Correct(req.MatchClockSeconds, req.Reason);
        return Ok(clock.State);
    }

    // ── Score ─────────────────────────────────────────────────────────────────

    [HttpGet("{matchId}/score")]
    public async Task<IActionResult> GetScore(string matchId, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        return Ok(new
        {
            homeGoals = match.HomeGoals, homePoints = match.HomePoints, homeTotal = match.HomeTotal,
            awayGoals = match.AwayGoals, awayPoints = match.AwayPoints, awayTotal = match.AwayTotal
        });
    }

    [HttpPost("{matchId}/score")]
    public async Task<IActionResult> UpdateScore(string matchId, [FromBody] ScoreUpdateRequest req, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();

        if (req.Team == "home")
        {
            if (req.ScoreType == "goal") match.HomeGoals++;
            else if (req.ScoreType == "point") match.HomePoints++;
        }
        else if (req.Team == "away")
        {
            if (req.ScoreType == "goal") match.AwayGoals++;
            else if (req.ScoreType == "point") match.AwayPoints++;
        }

        await repo.UpdateAsync(match, ct);
        overlay.UpdateScore(match.HomeGoals, match.HomePoints, match.AwayGoals, match.AwayPoints);
        return Ok(new
        {
            homeGoals = match.HomeGoals, homePoints = match.HomePoints, homeTotal = match.HomeTotal,
            awayGoals = match.AwayGoals, awayPoints = match.AwayPoints, awayTotal = match.AwayTotal
        });
    }
}

public record ClockCorrectRequest(int MatchClockSeconds, string? Reason);
public record ScoreUpdateRequest(string ScoreType, string Team);
