using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/cameras")]
public class CamerasController(
    ICameraRepository repo,
    ICameraAdapter cameraAdapter,
    IPtzController ptz,
    ICameraDiscoveryService discovery,
    ILogger<CamerasController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCameras(CancellationToken ct) =>
        Ok(await repo.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateCamera([FromBody] Camera input, CancellationToken ct)
    {
        input.CameraId = Guid.NewGuid().ToString();
        input.CreatedAt = input.UpdatedAt = DateTimeOffset.UtcNow;
        var created = await repo.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetCamera), new { cameraId = created.CameraId }, created);
    }

    [HttpGet("{cameraId}")]
    public async Task<IActionResult> GetCamera(string cameraId, CancellationToken ct)
    {
        var cam = await repo.GetByIdAsync(cameraId, ct);
        return cam is null ? NotFound() : Ok(cam);
    }

    [HttpPut("{cameraId}")]
    public async Task<IActionResult> UpdateCamera(string cameraId, [FromBody] Camera input, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(cameraId, ct);
        if (existing is null) return NotFound();
        input.CameraId = cameraId;
        input.CreatedAt = existing.CreatedAt;
        return Ok(await repo.UpdateAsync(input, ct));
    }

    [HttpDelete("{cameraId}")]
    public async Task<IActionResult> DeleteCamera(string cameraId, CancellationToken ct)
    {
        await repo.DeleteAsync(cameraId, ct);
        return NoContent();
    }

    [HttpPost("{cameraId}/connect")]
    public async Task<IActionResult> ConnectCamera(string cameraId, CancellationToken ct)
    {
        try
        {
            await cameraAdapter.ConnectAsync(ct);
            return Ok(new { success = true, connectionState = cameraAdapter.ConnectionState.ToString().ToLower() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect camera {Id}", cameraId);
            return Ok(new { success = false, error = ex.Message, connectionState = cameraAdapter.ConnectionState.ToString().ToLower() });
        }
    }

    [HttpPost("{cameraId}/disconnect")]
    public async Task<IActionResult> DisconnectCamera(string cameraId, CancellationToken ct)
    {
        await cameraAdapter.DisconnectAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{cameraId}/test")]
    public async Task<IActionResult> TestCamera(string cameraId, CancellationToken ct)
    {
        try
        {
            await cameraAdapter.ConnectAsync(ct);
            var info = await cameraAdapter.GetCameraInfoAsync(ct);
            return Ok(new { success = true, cameraInfo = info });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{cameraId}/ptz")]
    public async Task<IActionResult> SendPtzCommand(string cameraId, [FromBody] PtzCommandRequest cmd, CancellationToken ct)
    {
        try
        {
            switch (cmd.Action.ToLowerInvariant())
            {
                case "pan":
                    var panDir = cmd.Speed > 0 ? PtzDirection.Right : PtzDirection.Left;
                    await ptz.PanAsync(panDir, Math.Abs(cmd.Speed ?? 0.5), ct);
                    break;
                case "tilt":
                    var tiltDir = cmd.Speed > 0 ? PtzDirection.Up : PtzDirection.Down;
                    await ptz.TiltAsync(tiltDir, Math.Abs(cmd.Speed ?? 0.5), ct);
                    break;
                case "zoom":
                    var zoomDir = cmd.Speed > 0 ? ZoomDirection.In : ZoomDirection.Out;
                    await ptz.ZoomAsync(zoomDir, Math.Abs(cmd.Speed ?? 0.5), ct);
                    break;
                case "stop": await ptz.StopAsync(ct); break;
                case "home": await ptz.GoHomeAsync(ct); break;
                case "absolute":
                    await ptz.MoveAbsoluteAsync(cmd.Pan ?? 0, cmd.Tilt ?? 0, cmd.Zoom ?? 1, ct);
                    break;
                case "emergency_wide": await ptz.EmergencyWideAsync(ct); break;
            }
            return Ok(new { success = true });
        }
        catch (Exception ex) { return Ok(new { success = false, error = ex.Message }); }
    }

    [HttpGet("{cameraId}/presets")]
    public async Task<IActionResult> GetPresets(string cameraId, CancellationToken ct) =>
        Ok(await ptz.GetPresetsAsync(ct));

    [HttpPost("{cameraId}/presets")]
    public async Task<IActionResult> SavePreset(string cameraId, [FromBody] SavePresetRequest req, CancellationToken ct)
    {
        await ptz.SavePresetAsync(req.PresetId, req.Name, ct);
        return Ok(new { success = true });
    }

    [HttpPost("discover")]
    public async Task<IActionResult> Discover([FromBody] DiscoverRequest? req, CancellationToken ct)
    {
        var results = await discovery.ScanSubnetAsync(req?.Subnet, null, ct);
        return Ok(results);
    }
}

public record PtzCommandRequest(string Action, double? Speed, double? Pan, double? Tilt, double? Zoom);
public record SavePresetRequest(string PresetId, string Name);
public record DiscoverRequest(string? Subnet);
