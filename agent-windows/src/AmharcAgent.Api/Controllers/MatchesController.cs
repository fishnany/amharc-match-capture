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
    IScoringService scoring,
    IOverlayService overlay,
    IBroadcastService broadcast,
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
        input.UpdatedAt = DateTimeOffset.UtcNow;
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
        match.UpdatedAt = DateTimeOffset.UtcNow;
        await repo.UpdateAsync(match, ct);

        clock.Start();
        var score = scoring.GetState(match);
        overlay.UpdateScore(score);
        overlay.ShowScoreboard();
        broadcast.SetMatch(matchId);
        broadcast.UpdateScore(score);
        broadcast.ShowScoreBug();

        logger.LogInformation("Match {Id} started using scoring model {ScoringModel}", matchId, score.ScoringModel);
        return Ok(match);
    }

    [HttpPost("{matchId}/stop")]
    public async Task<IActionResult> StopMatch(string matchId, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        match.Status = MatchStatus.Complete;
        match.UpdatedAt = DateTimeOffset.UtcNow;
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
        return match is null ? NotFound() : Ok(scoring.GetState(match));
    }

    [HttpPut("{matchId}/score")]
    public async Task<IActionResult> UpdateScore(string matchId, [FromBody] ScoreUpdateRequest req, CancellationToken ct)
    {
        var match = await repo.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();

        var team = req.Team.Trim().ToLowerInvariant() switch
        {
            "home" => EventTeam.Home,
            "away" => EventTeam.Away,
            _ => throw new ArgumentException("Team must be 'home' or 'away'.", nameof(req.Team))
        };

        try
        {
            scoring.Apply(match, team, req.ScoreType, req.Delta);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        await repo.UpdateAsync(match, ct);
        var state = scoring.GetState(match);
        overlay.UpdateScore(state);
        broadcast.UpdateScore(state);
        return Ok(state);
    }
}

public record ClockCorrectRequest(int MatchClockSeconds, string? Reason);
public record ScoreUpdateRequest(string ScoreType, string Team, int Delta = 1, string? Reason = null);
