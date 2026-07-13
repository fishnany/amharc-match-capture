using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/recording")]
public class RecordingController(
    IRecordingService recording,
    ICameraAdapter camera,
    AmharcAgent.Core.Domain.AgentSettings settings,
    ILogger<RecordingController> logger) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartRecording([FromBody] StartRecordingRequest req, CancellationToken ct)
    {
        try
        {
            var rtspUrl = await camera.GetStreamUrlAsync(null, ct);
            var outputDir = req.OutputDirectory
                ?? Path.Combine(settings.RecordingDirectory, req.MatchId, DateTime.UtcNow.ToString("yyyyMMdd"));

            var opts = new RecordingOptions(
                req.MatchId, req.CameraId ?? camera.CameraId, rtspUrl,
                outputDir, settings.SegmentDurationSeconds, true);

            await recording.StartRecordingAsync(opts, ct);
            logger.LogInformation("Recording started for match {MatchId}", req.MatchId);
            return Ok(new { state = recording.State.ToString().ToLower(), outputDirectory = outputDir });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start recording");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("stop")]
    public async Task<IActionResult> StopRecording(CancellationToken ct)
    {
        await recording.StopRecordingAsync(ct);
        return Ok(new { state = recording.State.ToString().ToLower(), segments = recording.GetSegments() });
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

public record StartRecordingRequest(string MatchId, string? CameraId, string? OutputDirectory);
