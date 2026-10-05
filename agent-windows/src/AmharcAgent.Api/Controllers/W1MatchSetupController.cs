using System.Text.Json.Nodes;
using AmharcAgent.Api.W1;
using AmharcAgent.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/w1/match-setups")]
public sealed class W1MatchSetupController(IConfiguration configuration,
    IWebHostEnvironment environment, IServiceProvider services) : ControllerBase
{
    private bool Enabled => environment.IsDevelopment() &&
        configuration.GetValue<bool>("W1:DevelopmentOnly") &&
        configuration.GetValue<bool>("W1:MatchSetupConformance");
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (Request.ContentLength is > 1048576) return StatusCode(413);
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        if (System.Text.Encoding.UTF8.GetByteCount(raw) > 1048576) return StatusCode(413);
        try
        {
            var result = services.GetRequiredService<W1MatchSetupApplication>().Receive(raw,
                Request.Headers["X-W1-Tagger-Issuer"].ToString(), Request.Headers["X-W1-Origin-Signature"].ToString());
            var status = W1MatchSetupAdmission.Text(result["status"]);
            return StatusCode(status == "SUCCEEDED" ? 200 : status == "INDETERMINATE" ? 503 : 409, result);
        }
        catch (Exception e) when (e is FormatException or System.Text.Json.JsonException)
        { return BadRequest(new { code = "INVALID_REQUEST" }); }
    }
    [HttpPost("{subject}/prepare")]
    public IActionResult Prepare(string subject)
    {
        if (!Enabled) return NotFound();
        var setups = services.GetRequiredService<W1MatchSetupApplication>();
        var setup = setups.Ledger.Read(subject);
        if (setup is null) return NotFound();
        try
        {
            var app = services.GetRequiredService<W1DevelopmentApplication>();
            return Ok(app.PrepareMatchSetup(setup, setups.Ledger.Closure()));
        }
        catch (InvalidOperationException e) { return Conflict(new { code = e.Message }); }
    }
}