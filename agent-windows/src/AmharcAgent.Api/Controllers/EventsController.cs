using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/matches/{matchId}/events")]
public class EventsController(
    IEventTaggingService eventService,
    IMatchClockService clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEvents(string matchId, CancellationToken ct) =>
        Ok(await eventService.GetEventsAsync(matchId, ct));

    [HttpPost]
    public async Task<IActionResult> CreateEvent(
        string matchId, [FromBody] CreateEventRequest req, CancellationToken ct)
    {
        var clockState = clock.State;
        var opts = new CreateEventOptions(
            MatchId: matchId,
            EventType: req.EventType,
            Team: req.Team,
            PlayerNumber: req.PlayerNumber,
            Period: req.Period ?? clockState.CurrentPeriod,
            MatchClockSeconds: req.MatchClockSeconds ?? clockState.MatchClockSeconds,
            RecordingElapsedSeconds: req.RecordingElapsedSeconds ?? clockState.RecordingElapsedSeconds,
            Source: req.Source ?? EventSource.OperatorUi,
            Note: req.Note,
            ClipRequested: req.ClipRequested ?? false,
            Operator: req.Operator);

        var evt = await eventService.CreateEventAsync(opts, ct);
        return CreatedAtAction(nameof(GetEvent), new { matchId, eventId = evt.EventId }, evt);
    }

    [HttpGet("{eventId}")]
    public async Task<IActionResult> GetEvent(string matchId, string eventId, CancellationToken ct)
    {
        var events = await eventService.GetEventsAsync(matchId, ct);
        var evt = events.FirstOrDefault(e => e.EventId == eventId);
        return evt is null ? NotFound() : Ok(evt);
    }

    [HttpPut("{eventId}")]
    public async Task<IActionResult> UpdateEvent(
        string matchId, string eventId, [FromBody] MatchEvent input, CancellationToken ct) =>
        Ok(await eventService.UpdateEventAsync(eventId, input, ct));

    [HttpDelete("{eventId}")]
    public async Task<IActionResult> DeleteEvent(string matchId, string eventId, CancellationToken ct)
    {
        await eventService.DeleteEventAsync(eventId, ct);
        return NoContent();
    }

    [HttpPost("undo")]
    public async Task<IActionResult> UndoLastEvent(string matchId, CancellationToken ct)
    {
        var evt = await eventService.UndoLastEventAsync(matchId, ct);
        return evt is null ? Ok(new { message = "No events to undo" }) : Ok(evt);
    }
}

public record CreateEventRequest(
    string EventType,
    EventTeam? Team,
    int? PlayerNumber,
    int? Period,
    int? MatchClockSeconds,
    int? RecordingElapsedSeconds,
    EventSource? Source,
    string? Note,
    bool? ClipRequested,
    string? Operator);
