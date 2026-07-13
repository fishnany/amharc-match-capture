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
        if (Enum.TryParse<OverlayOutputMode>(req.Mode, true, out var mode))
            overlay.SetOutputMode(mode);
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
