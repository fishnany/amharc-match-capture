using AmharcAgent.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api")]
public class SystemController(
    IHealthMonitoringService health,
    ICameraAdapter camera,
    IRecordingService recording,
    IStreamingService streaming,
    IStreamDeckService streamDeck,
    IJoystickService joystick,
    IStorageMonitorService storage,
    IOverlayService overlay) : ControllerBase
{
    [HttpGet("healthz")]
public IActionResult HealthCheck()
{
    var systemHealth = health.GetHealth();

    return Ok(new
    {
        status = systemHealth.OverallState.ToString().ToLowerInvariant(),
        version = "1.0.0",
        timestamp = DateTimeOffset.UtcNow,
        health = systemHealth
    });
}

    [HttpGet("system/status")]
    public IActionResult GetSystemStatus() => Ok(new
    {
        camera = new
        {
            connectionState = camera.ConnectionState.ToString().ToLowerInvariant(),
            cameraId = camera.CameraId,
            model = camera.Model
        },
        recording = new
        {
            state = recording.State.ToString().ToLowerInvariant(),
            elapsedSeconds = (int)recording.ElapsedSeconds,
            segmentCount = recording.SegmentCount,
            outputDirectory = recording.OutputDirectory
        },
        streaming = new
        {
            state = streaming.State.ToString().ToLowerInvariant(),
            stats = streaming.Stats
        },
        streamDeck = new
        {
            connected = streamDeck.IsConnected,
            deviceName = streamDeck.DeviceName,
            activeProfileId = streamDeck.ActiveProfileId
        },
        joystick = new
        {
            connected = joystick.IsConnected,
            deviceName = joystick.DeviceName
        },
        storage = new
        {
            availableMinutes = (int)storage.Status.AvailableMinutes,
            warningLevel = storage.Status.WarningLevel.ToString().ToLowerInvariant(),
            availableBytes = storage.Status.AvailableBytes
        },
        overlay = new
        {
            isVisible = overlay.State.IsVisible,
            outputMode = overlay.State.OutputMode.ToString().ToLowerInvariant(),
            activeTemplateId = overlay.State.ActiveTemplateId
        },
        audio = new { state = "unknown" }
    });
}
