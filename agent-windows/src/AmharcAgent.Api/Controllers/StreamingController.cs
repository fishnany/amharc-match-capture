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
    AmharcDbContext db,
    ILogger<StreamingController> logger) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartStreaming([FromBody] StartStreamingRequest req, CancellationToken ct)
    {
        var dest = await db.StreamingDestinations.FindAsync([req.DestinationId], ct);
        if (dest is null) return NotFound(new { error = $"Destination {req.DestinationId} not found" });

        var config = new StreamingDestinationConfig(
            dest.DestinationId, dest.Platform.ToString(), dest.ServerUrl, dest.StreamKey,
            dest.Resolution, dest.FrameRate, dest.BitRate);

        await streaming.StartAsync(config, ct);
        return Ok(new { state = streaming.State.ToString().ToLower() });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> StopStreaming(CancellationToken ct)
    {
        await streaming.StopAsync(ct);
        return Ok(new { state = streaming.State.ToString().ToLower() });
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new
    {
        state = streaming.State.ToString().ToLower(),
        stats = streaming.Stats
    });

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
}

public record StartStreamingRequest(string DestinationId);
