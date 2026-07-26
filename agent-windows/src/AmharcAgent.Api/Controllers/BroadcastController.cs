using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/broadcast")]
public sealed class BroadcastController(
    IBroadcastService broadcast,
    IScoreBugRenderer scoreBugRenderer,
    IEventBannerRenderer eventBannerRenderer,
    IMatchIntroRenderer matchIntroRenderer,
    IMatchRepository matches,
    IEventRepository events) : ControllerBase
{
    [HttpGet("state")]
    public IActionResult GetState() => Ok(broadcast.State);

    [HttpGet("score-bug.svg")]
    public async Task<IActionResult> GetScoreBug([FromQuery] string? matchId, CancellationToken ct)
    {
        var id = matchId ?? broadcast.State.MatchId;
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "No active match is selected." });

        var match = await matches.GetByIdAsync(id, ct);
        if (match is null) return NotFound();

        var score = broadcast.State.Score is { MatchId: var scoreMatch } && scoreMatch == id
            ? broadcast.State.Score
            : new ScoreState(
                match.MatchId, match.Sport, match.ScoringModel,
                match.HomeGoals, match.HomeTwoPointScores, match.HomePoints,
                match.AwayGoals, match.AwayTwoPointScores, match.AwayPoints,
                match.UpdatedAt);

        var discipline = await ResolveDisciplineAsync(id, ct);
        var model = new ScoreBugViewModel(
            match.MatchId,
            match.Sport,
            score.ScoringModel,
            match.HomeTeam,
            Abbreviate(match.HomeTeam),
            score.Home,
            discipline.Home,
            match.AwayTeam,
            Abbreviate(match.AwayTeam),
            score.Away,
            discipline.Away,
            broadcast.State.MatchClockSeconds,
            broadcast.State.CurrentPeriod,
            match.Status,
            string.Equals(broadcast.State.CurrentGraphic, "replay", StringComparison.OrdinalIgnoreCase) && broadcast.State.GraphicVisible,
            "/branding/amharc-logo-transparent.png",
            broadcast.State.Theme);

        var svg = scoreBugRenderer.RenderSvg(model);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(svg, "image/svg+xml; charset=utf-8");
    }


    [HttpGet("event-banner.svg")]
    public async Task<IActionResult> GetEventBanner(
        [FromQuery] string matchId,
        [FromQuery] string eventType,
        [FromQuery] EventTeam? team,
        [FromQuery] int? playerNumber,
        CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        if (string.IsNullOrWhiteSpace(eventType)) return BadRequest(new { message = "eventType is required." });
        var teamName = team == EventTeam.Away ? match.AwayTeam : match.HomeTeam;
        var model = new EventBannerViewModel(match.MatchId, match.Sport, eventType, teamName,
            Abbreviate(teamName), playerNumber, null, "/branding/amharc-logo-transparent.png", broadcast.State.Theme);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(eventBannerRenderer.RenderSvg(model), "image/svg+xml; charset=utf-8");
    }

    [HttpGet("match-intro.svg")]
    public async Task<IActionResult> GetMatchIntro([FromQuery] string matchId, [FromQuery] string? throwIn, CancellationToken ct)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null) return NotFound();
        var model = new MatchIntroViewModel(match.MatchId, match.Sport, match.Competition, match.Round,
            match.HomeTeam, match.AwayTeam, match.Venue, match.Date, throwIn ?? "THROW-IN", 
            "/branding/amharc-logo-transparent.png", broadcast.State.Theme);
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Content(matchIntroRenderer.RenderSvg(model), "image/svg+xml; charset=utf-8");
    }

    private async Task<(TeamDisciplineState Home, TeamDisciplineState Away)> ResolveDisciplineAsync(string matchId, CancellationToken ct)
    {
        var all = (await events.GetByMatchIdAsync(matchId, ct)).OrderBy(e => e.MatchClockSeconds).ThenBy(e => e.CreatedAt).ToList();
        return (ResolveTeam(all, EventTeam.Home), ResolveTeam(all, EventTeam.Away));
    }

    private static TeamDisciplineState ResolveTeam(IEnumerable<MatchEvent> source, EventTeam team)
    {
        var teamEvents = source.Where(e => e.Team == team).ToList();
        var red = teamEvents.Any(e => Normalize(e.EventType) == "red_card");

        // A black card remains visible only while an affected player is still serving it.
        // Duration itself is governed by match events, not hard-coded into the renderer.
        var activeBlackPlayers = new HashSet<int?>();
        foreach (var e in teamEvents)
        {
            var type = Normalize(e.EventType);
            if (type == "black_card") activeBlackPlayers.Add(e.PlayerNumber);
            if (type is "black_card_end" or "black_card_expired" or "player_return")
            {
                if (e.PlayerNumber is null) activeBlackPlayers.Clear();
                else activeBlackPlayers.Remove(e.PlayerNumber);
            }
        }

        return new TeamDisciplineState(red, activeBlackPlayers.Count > 0);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

    private static string Abbreviate(string team)
    {
        var letters = new string(team.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return letters.Length <= 3 ? letters : letters[..3];
    }
}
