using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController(
    IStreamDeckService streamDeck,
    IJoystickService joystick,
    AmharcDbContext db) : ControllerBase
{
    [HttpGet("stream-deck")]
    public IActionResult GetStreamDeckStatus() => Ok(new
    {
        connected = streamDeck.IsConnected,
        deviceName = streamDeck.DeviceName,
        activeProfileId = streamDeck.ActiveProfileId
    });

    [HttpGet("joystick")]
    public IActionResult GetJoystickStatus() => Ok(new
    {
        connected = joystick.IsConnected,
        deviceName = joystick.DeviceName
    });

    [HttpGet("stream-deck/profiles")]
    public async Task<IActionResult> GetProfiles(
        CancellationToken ct) =>
        Ok(await db.StreamDeckProfiles.ToListAsync(ct));

    [HttpPost("stream-deck/profiles")]
    public async Task<IActionResult> CreateProfile(
        [FromBody] StreamDeckProfile profile,
        CancellationToken ct)
    {
        profile.ProfileId = Guid.NewGuid().ToString();
        profile.CreatedAt = DateTimeOffset.UtcNow;
        profile.UpdatedAt = profile.CreatedAt;

        db.StreamDeckProfiles.Add(profile);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(
            nameof(GetProfile),
            new { profileId = profile.ProfileId },
            profile);
    }

    [HttpGet("stream-deck/profiles/{profileId}")]
    public async Task<IActionResult> GetProfile(
        string profileId,
        CancellationToken ct)
    {
        var profile = await db.StreamDeckProfiles
            .FindAsync([profileId], ct);

        return profile is null
            ? NotFound()
            : Ok(profile);
    }

    [HttpPut("stream-deck/profiles/{profileId}")]
    public async Task<IActionResult> UpdateProfile(
        string profileId,
        [FromBody] StreamDeckProfile input,
        CancellationToken ct)
    {
        var profile = await db.StreamDeckProfiles
            .FindAsync([profileId], ct);

        if (profile is null)
            return NotFound();

        profile.Name = input.Name;
        profile.Sport = input.Sport;
        profile.Buttons = input.Buttons;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        if (streamDeck.ActiveProfileId == profileId)
        {
            await streamDeck.LoadProfileAsync(
                profile,
                ct);
        }

        return Ok(profile);
    }

    [HttpPost("stream-deck/profiles/{profileId}/activate")]
    public async Task<IActionResult> ActivateProfile(
        string profileId,
        CancellationToken ct)
    {
        var profile = await db.StreamDeckProfiles
            .FindAsync([profileId], ct);

        if (profile is null)
            return NotFound();

        await streamDeck.LoadProfileAsync(
            profile,
            ct);

        return Ok(new
        {
            activeProfileId = profileId
        });
    }
}