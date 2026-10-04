using AmharcAgent.Infrastructure.Clock;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

/// <summary>Separate, default-disabled development boundary. Composition of
/// verified dependencies, development grants and scratch stores is explicit;
/// missing composition cannot fall back to legacy runtime identity/authority.</summary>
[ApiController]
[Route("api/w1-development/clock")]
public sealed class W1DevelopmentClockController(
    IConfiguration configuration, IWebHostEnvironment environment,
    IServiceProvider services) : ControllerBase
{
    [HttpGet("{occurrenceId}")]
    public IActionResult Observe(string occurrenceId)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("W1:DevelopmentOnly"))
            return NotFound();
        var producer = services.GetService<W1GovernedClockProducer>();
        if (producer is null) return Conflict(new { code = "W1_DEVELOPMENT_CONTEXT_UNRESOLVED" });
        try { return Ok(producer.Emit(occurrenceId)); }
        catch (InvalidOperationException e) { return Conflict(new { code = e.Message }); }
    }
}