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
    IProtectedCredentialStore credentials) : ControllerBase
{
    [HttpPost("start")]
    [ProducesResponseType<StreamingStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StreamingStatusResponse>> StartStreaming([FromBody] StartStreamingRequest req, CancellationToken ct)
    {
        var dest = await db.StreamingDestinations.FindAsync([req.DestinationId], ct);
        if (dest is null) return NotFound(new { error = $"Destination {req.DestinationId} not found" });

        var protectedCredential =
            await credentials.ReadAsync(StreamCredentialTarget(dest.DestinationId), ct);
        if (protectedCredential is null || string.IsNullOrEmpty(protectedCredential.Secret))
            return Conflict(new { error = "Streaming credential is not available for this destination" });

        var config = new StreamingDestinationConfig(
            dest.DestinationId, dest.Platform.ToString(), dest.ServerUrl, protectedCredential.Secret,
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
    public async Task<IActionResult> GetDestinations(CancellationToken ct)
    {
        var destinations = await db.StreamingDestinations.ToListAsync(ct);
        var responses = new List<StreamingDestinationResponse>(destinations.Count);
        foreach (var destination in destinations)
        {
            var credential =
                await credentials.ReadAsync(StreamCredentialTarget(destination.DestinationId), ct);
            responses.Add(ToResponse(destination, credential is not null));
        }
        return Ok(responses);
    }

    [HttpPost("destinations")]
    public async Task<IActionResult> CreateDestination(
        [FromBody] StreamingDestinationWriteRequest input,
        CancellationToken ct)
    {
        var destinationId = Guid.NewGuid().ToString();
        if (!string.IsNullOrEmpty(input.StreamKey))
        {
            await credentials.WriteAsync(
                StreamCredentialTarget(destinationId),
                new ProtectedCredential(string.Empty, input.StreamKey),
                ct);
        }

        var now = DateTimeOffset.UtcNow;
        var destination = new StreamingDestination
        {
            DestinationId = destinationId,
            Platform = input.Platform,
            Name = input.Name,
            ServerUrl = input.ServerUrl,
            StreamKey = string.Empty,
            Resolution = input.Resolution,
            FrameRate = input.FrameRate,
            BitRate = input.BitRate,
            IsActive = input.IsDefault,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            db.StreamingDestinations.Add(destination);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (!string.IsNullOrEmpty(input.StreamKey))
                await credentials.DeleteAsync(StreamCredentialTarget(destinationId), CancellationToken.None);
            throw;
        }

        return CreatedAtAction(
            null,
            null,
            ToResponse(destination, hasStreamKey: !string.IsNullOrEmpty(input.StreamKey)));
    }

    [HttpDelete("destinations/{destinationId}")]
    public async Task<IActionResult> DeleteDestination(string destinationId, CancellationToken ct)
    {
        var dest = await db.StreamingDestinations.FindAsync([destinationId], ct);
        if (dest is null) return NotFound();

        db.StreamingDestinations.Remove(dest);
        await db.SaveChangesAsync(ct);
        await credentials.DeleteAsync(StreamCredentialTarget(destinationId), ct);
        return NoContent();
    }

    private static string StreamCredentialTarget(string destinationId) =>
        $"AMHARC/Streaming/{destinationId}";

    private static StreamingDestinationResponse ToResponse(
        StreamingDestination destination,
        bool hasStreamKey) =>
        new(
            destination.DestinationId,
            destination.Platform,
            destination.Name,
            destination.ServerUrl,
            destination.Resolution,
            destination.FrameRate,
            destination.BitRate,
            destination.IsActive,
            destination.CreatedAt,
            destination.UpdatedAt,
            hasStreamKey);

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

public sealed record StreamingDestinationWriteRequest(
    StreamingPlatform Platform,
    string Name,
    string ServerUrl,
    string? StreamKey,
    string? Resolution,
    int? FrameRate,
    int? BitRate,
    bool IsDefault);

public sealed record StreamingDestinationResponse(
    string DestinationId,
    StreamingPlatform Platform,
    string Name,
    string ServerUrl,
    string? Resolution,
    int? FrameRate,
    int? BitRate,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasStreamKey);
