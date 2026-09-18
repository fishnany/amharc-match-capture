using AmharcAgent.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/overlays")]
public class OverlaysController(IOverlayService overlay) : ControllerBase
{
    [HttpGet("state")]
    public IActionResult GetState() => Ok(overlay.State);

    [HttpPost("show")]
    public IActionResult Show() { overlay.ShowScoreboard(); return Ok(overlay.State); }

    [HttpPost("hide")]
    public IActionResult Hide() { overlay.HideScoreboard(); return Ok(overlay.State); }

    [HttpPost("mode")]
    public IActionResult SetMode([FromBody] SetModeRequest req)
    {
        var mode = req.Mode.Trim().ToLowerInvariant() switch
        {
            "clean" => OverlayOutputMode.Clean,
            "programme" => OverlayOutputMode.Programme,
            "overlay-only" => OverlayOutputMode.OverlayOnly,
            "operator-preview" => OverlayOutputMode.OperatorPreview,
            _ => (OverlayOutputMode?)null
        };

        if (mode is null)
            return BadRequest(new { error = $"Unsupported overlay output mode '{req.Mode}'." });

        overlay.SetOutputMode(mode.Value);
        return Ok(overlay.State);
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates() => Ok(new[]
    {
        new { templateId = "standard-scoreboard", name = "Standard Scoreboard", type = "scoreboard" },
        new { templateId = "lower-third", name = "Lower Third", type = "graphic" },
        new { templateId = "fullscreen-score", name = "Fullscreen Score", type = "scoreboard" }
    });
}

public record SetModeRequest(string Mode);
