using System.Text.Json.Nodes;
using AmharcAgent.Api.W1;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

/// <summary>Default-off synthetic fixture preparation, not recovery or signing.</summary>
[ApiController]
[Route("api/w1-development/installed-assurance-fixtures")]
public sealed class W1InstalledAssuranceFixtureController(
    IConfiguration configuration, IWebHostEnvironment environment, IServiceProvider services) : ControllerBase
{
    [HttpPost("{subject}")]
    public IActionResult Prepare(string subject, [FromBody] JsonObject request)
    {
        if (!environment.IsDevelopment() ||
            !configuration.GetValue<bool>("W1:DevelopmentOnly") ||
            !configuration.GetValue<bool>("W1:MatchSetupConformance") ||
            !configuration.GetValue<bool>("W1:InstalledAssuranceFixtures"))
            return NotFound();
        try
        {
            if (request.Count != 2 || !request.ContainsKey("scenario") ||
                !request.ContainsKey("operationKey") ||
                request["scenario"] is not JsonValue || request["operationKey"] is not JsonValue)
                throw new InvalidOperationException("W1_ASSURANCE_SCENARIO_REFUSAL");
            var result = services.GetRequiredService<W1DevelopmentApplication>()
                .PrepareInstalledAssuranceFixture(subject,
                    request["scenario"]!.GetValue<string>(), request["operationKey"]!.GetValue<string>(),
                    services.GetRequiredService<W1MatchSetupApplication>());
            return Ok(result);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.IO.IOException)
        { return Conflict(new { code = e.Message }); }
    }
}
