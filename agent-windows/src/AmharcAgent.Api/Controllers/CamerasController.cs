using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/cameras")]
public class CamerasController(
    ICameraRepository repo,
    ICameraAdapter cameraAdapter,
    IPtzController ptz,
    IPreviewService preview,
    ICameraDiscoveryService discovery,
    IProtectedCredentialStore credentials,
    ILogger<CamerasController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCameras(CancellationToken ct)
    {
        var cameras = await repo.GetAllAsync(ct);
        var responses = new List<CameraResponse>(cameras.Count);
        foreach (var camera in cameras)
        {
            var credential =
                await credentials.ReadAsync(CameraCredentialTarget(camera.CameraId), ct);
            responses.Add(ToResponse(camera, credential is not null));
        }
        return Ok(responses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCamera([FromBody] CameraWriteRequest input, CancellationToken ct)
    {
        var cameraId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        if (input.Password is not null)
        {
            await credentials.WriteAsync(
                CameraCredentialTarget(cameraId),
                new ProtectedCredential(input.Username, input.Password),
                ct);
        }

        try
        {
            var created = await repo.CreateAsync(ToCamera(input, cameraId, now, now), ct);
            return CreatedAtAction(
                nameof(GetCamera),
                new { cameraId = created.CameraId },
                ToResponse(created, input.Password is not null));
        }
        catch
        {
            if (input.Password is not null)
                await credentials.DeleteAsync(CameraCredentialTarget(cameraId), CancellationToken.None);
            throw;
        }
    }

    [HttpGet("{cameraId}")]
    public async Task<IActionResult> GetCamera(string cameraId, CancellationToken ct)
    {
        var cam = await repo.GetByIdAsync(cameraId, ct);
        if (cam is null) return NotFound();

        var protectedCredential =
            await credentials.ReadAsync(CameraCredentialTarget(cameraId), ct);

        return Ok(ToResponse(cam, protectedCredential is not null));
    }

    [HttpPut("{cameraId}")]
    public async Task<IActionResult> UpdateCamera(
        string cameraId,
        [FromBody] CameraWriteRequest input,
        CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(cameraId, ct);
        if (existing is null) return NotFound();

        var target = CameraCredentialTarget(cameraId);
        var previousCredential = await credentials.ReadAsync(target, ct);

        if (input.Password is not null)
        {
            await credentials.WriteAsync(
                target,
                new ProtectedCredential(input.Username, input.Password),
                ct);
        }

        try
        {
            var updated = ToCamera(input, cameraId, existing.CreatedAt, DateTimeOffset.UtcNow);
            var saved = await repo.UpdateAsync(updated, ct);
            var protectedCredential = await credentials.ReadAsync(target, ct);
            return Ok(ToResponse(saved, protectedCredential is not null));
        }
        catch
        {
            if (input.Password is not null)
            {
                if (previousCredential is null)
                    await credentials.DeleteAsync(target, CancellationToken.None);
                else
                    await credentials.WriteAsync(target, previousCredential, CancellationToken.None);
            }
            throw;
        }
    }

    [HttpDelete("{cameraId}")]
    public async Task<IActionResult> DeleteCamera(string cameraId, CancellationToken ct)
    {
        var existing = await repo.GetByIdAsync(cameraId, ct);
        if (existing is null) return NoContent();

        await repo.DeleteAsync(cameraId, ct);
        await credentials.DeleteAsync(CameraCredentialTarget(cameraId), ct);
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

    /// <summary>
    /// Streams the local operator preview as browser-compatible MJPEG.
    /// Camera credentials and the authenticated RTSP URL never leave the Agent.
    /// </summary>
    [HttpGet("active/preview")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task Preview(CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = preview.ContentType;
        Response.Headers.CacheControl =
            "no-store, no-cache, must-revalidate";

        try
        {
            await preview.StreamMjpegAsync(
                Response.Body,
                HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
            when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Normal browser disconnect/navigation.
        }
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

    private static string CameraCredentialTarget(string cameraId) =>
        $"AMHARC/Camera/{cameraId}";

    private static Camera ToCamera(
        CameraWriteRequest input,
        string cameraId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            CameraId = cameraId,
            Name = input.Name,
            Manufacturer = input.Manufacturer,
            Model = input.Model,
            IpAddress = input.IpAddress,
            RtspPort = input.RtspPort,
            HttpPort = input.HttpPort,
            Username = string.Empty,
            Password = string.Empty,
            Role = input.Role,
            StreamProfileName = input.StreamProfileName,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    private static CameraResponse ToResponse(Camera camera, bool hasCredentials = false) =>
        new(
            camera.CameraId,
            camera.Name,
            camera.Manufacturer,
            camera.Model,
            camera.IpAddress,
            camera.RtspPort,
            camera.HttpPort,
            camera.Role,
            camera.ConnectionState,
            camera.LastConnectedAt,
            camera.StreamProfileName,
            camera.SerialNumber,
            camera.FirmwareVersion,
            camera.MacAddress,
            camera.CreatedAt,
            camera.UpdatedAt,
            hasCredentials);

}

public record PtzCommandRequest(string Action, double? Speed, double? Pan, double? Tilt, double? Zoom);
public record SavePresetRequest(string PresetId, string Name);
public record DiscoverRequest(string? Subnet);

public sealed record CameraWriteRequest(
    string Name,
    string Manufacturer,
    string Model,
    string IpAddress,
    int RtspPort,
    int HttpPort,
    string Username,
    string? Password,
    CameraRole Role,
    string? StreamProfileName);

public sealed record CameraResponse(
    string CameraId,
    string Name,
    string Manufacturer,
    string Model,
    string IpAddress,
    int RtspPort,
    int HttpPort,
    CameraRole Role,
    CameraConnectionState ConnectionState,
    DateTimeOffset? LastConnectedAt,
    string? StreamProfileName,
    string? SerialNumber,
    string? FirmwareVersion,
    string? MacAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasCredentials);
