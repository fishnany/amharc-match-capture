using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/streaming")]
public class StreamingController(
    IStreamingService streaming,
    AmharcDbContext db) : ControllerBase
{
    [HttpPost("start")]
    [ProducesResponseType<StreamingStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StreamingStatusResponse>> StartStreaming([FromBody] StartStreamingRequest req, CancellationToken ct)
    {
        var dest = await db.StreamingDestinations.FindAsync([req.DestinationId], ct);
        if (dest is null) return NotFound(new { error = $"Destination {req.DestinationId} not found" });

        var config = new StreamingDestinationConfig(
            dest.DestinationId, dest.Platform.ToString(), dest.ServerUrl, dest.StreamKey,
            dest.Resolution, dest.FrameRate, dest.BitRate);

        await streaming.StartAsync(config, ct);
        return Ok(BuildStatus());
    }

    [HttpPost("stop")]
    [ProducesResponseType<StreamingStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StreamingStatusResponse>> StopStreaming(CancellationToken ct)
    {
        await streaming.StopAsync(ct);
        return Ok(BuildStatus());
    }

    [HttpGet("status")]
    [ProducesResponseType<StreamingStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<StreamingStatusResponse> GetStatus() => Ok(BuildStatus());

    [HttpGet("destinations")]
    public async Task<IActionResult> GetDestinations(CancellationToken ct) =>
        Ok(await db.StreamingDestinations.ToListAsync(ct));

    [HttpPost("destinations")]
    public async Task<IActionResult> CreateDestination([FromBody] StreamingDestination dest, CancellationToken ct)
    {
        dest.DestinationId = Guid.NewGuid().ToString();
        dest.CreatedAt = dest.UpdatedAt = DateTimeOffset.UtcNow;
        db.StreamingDestinations.Add(dest);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(null, null, dest);
    }

    [HttpDelete("destinations/{destinationId}")]
    public async Task<IActionResult> DeleteDestination(string destinationId, CancellationToken ct)
    {
        var dest = await db.StreamingDestinations.FindAsync([destinationId], ct);
        if (dest is null) return NotFound();
        db.StreamingDestinations.Remove(dest);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private StreamingStatusResponse BuildStatus()
    {
        var stats = streaming.Stats;
        var state = streaming.State.ToString().ToLowerInvariant();
        var isStreaming = streaming.State is StreamingState.Connecting or StreamingState.Streaming or StreamingState.Reconnecting;

        return new StreamingStatusResponse(
            state,
            isStreaming,
            streaming.ActiveDestinationId,
            stats?.UptimeSeconds,
            stats?.OutgoingBitRate,
            stats?.DroppedFrames,
            stats?.ReconnectCount ?? 0,
            streaming.LastError,
            streaming.StartedAt);
    }
}

public record StartStreamingRequest(string DestinationId);
public record StreamingStatusResponse(
    string State,
    bool IsStreaming,
    string? Destination,
    double? UptimeSeconds,
    double? OutgoingBitRate,
    int? DroppedFrames,
    int ReconnectCount,
    string? Error,
    DateTimeOffset? StartedAt);
