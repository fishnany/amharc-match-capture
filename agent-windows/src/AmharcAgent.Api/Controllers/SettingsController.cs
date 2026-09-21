using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(
    AgentSettings settings,
    IJoystickService joystickService,
    IAgentSettingsStore settingsStore) : ControllerBase
{
    [HttpGet("joystick")]
    public ActionResult<JoystickConfig> GetJoystickSettings()
    {
        return Ok(settings.Joystick);
    }

    [HttpPut("joystick")]
    public async Task<ActionResult<JoystickConfig>>
        UpdateJoystickSettings(
            [FromBody] JoystickConfig input,
            CancellationToken cancellationToken)
    {
        var validated = Validate(input);
        var previous = settings.Joystick;

        try
        {
            settings.Joystick = validated;

            await settingsStore.SaveAsync(
                settings,
                cancellationToken);

            joystickService.UpdateConfig(validated);

            return Ok(validated);
        }
        catch
        {
            settings.Joystick = previous;
            joystickService.UpdateConfig(previous);

            throw;
        }
    }

    private static JoystickConfig Validate(JoystickConfig input)
    {
        return input with
        {
            DeadZone = Math.Clamp(
                input.DeadZone,
                0.0,
                0.5),

            PanSensitivity = Math.Clamp(
                input.PanSensitivity,
                0.1,
                2.0),

            TiltSensitivity = Math.Clamp(
                input.TiltSensitivity,
                0.1,
                2.0),

            ZoomSensitivity = Math.Clamp(
                input.ZoomSensitivity,
                0.1,
                2.0),

            PtzUpdateIntervalMs = Math.Clamp(
                input.PtzUpdateIntervalMs,
                20,
                500),

            ResponseCurveStrength = Math.Clamp(
                input.ResponseCurveStrength,
                0.1,
                3.0)
        };
    }
}