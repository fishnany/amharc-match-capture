using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/recording")]
public class RecordingController(
    IAmharcCommandDispatcher commandDispatcher,
    IRecordingService recording,
    ILogger<RecordingController> logger) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartRecording(
        [FromBody] StartRecordingRequest req,
        CancellationToken ct)
    {
        try
        {
            var parameters = new Dictionary<string, string?>
            {
                ["cameraId"] = req.CameraId,
                ["outputDirectory"] = req.OutputDirectory
            };

            await commandDispatcher.DispatchAsync(
                new AmharcCommand(
                    AmharcCommandIds.RecordingStart,
                    EventSource.Api,
                    MatchId: req.MatchId,
                    Parameters: parameters),
                ct);

            logger.LogInformation(
                "Recording start command dispatched for match {MatchId}",
                req.MatchId);

            return Ok(new
            {
                state = recording.State.ToString().ToLower(),
                outputDirectory = recording.OutputDirectory
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start recording");

            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    [HttpPost("stop")]
    public async Task<IActionResult> StopRecording(CancellationToken ct)
    {
        await commandDispatcher.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.RecordingStop,
                EventSource.Api),
            ct);

        return Ok(new
        {
            state = recording.State.ToString().ToLower(),
            segments = recording.GetSegments()
        });
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new
    {
        state = recording.State.ToString().ToLower(),
        elapsedSeconds = (int)recording.ElapsedSeconds,
        segmentCount = recording.SegmentCount,
        outputDirectory = recording.OutputDirectory,
        segments = recording.GetSegments()
    });
}

public record StartRecordingRequest(
    string MatchId,
    string? CameraId,
    string? OutputDirectory);
