using System.Text.Json.Nodes;
using AmharcAgent.Api.W1;
using AmharcAgent.Data;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/w1-development")]
public sealed class W1DevelopmentClockController(
    IConfiguration configuration, IWebHostEnvironment environment, IServiceProvider services) : ControllerBase
{
    private IActionResult Run(Func<W1DevelopmentApplication, object?> action)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("W1:DevelopmentOnly")) return NotFound();
        try
        {
            var app = services.GetService<W1DevelopmentApplication>()
                ?? throw new InvalidOperationException("W1_DEVELOPMENT_CONTEXT_UNRESOLVED");
            return Ok(action(app));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.IO.IOException)
        { return Conflict(new { code = e.Message }); }
    }
    [HttpGet("clock/{subject}")]
    public IActionResult Observe(string subject) => Run(app => app.Observe(subject));
    [HttpGet("context")]
    public IActionResult Context() => Run(app => app.Dependencies());
    [HttpGet("history/{subject}")]
    public IActionResult History(string subject) => Run(app => app.History(subject));
    [HttpPost("prepare/{localId}")]
    public async Task<IActionResult> Prepare(string localId, [FromBody] JsonObject resolution, CancellationToken ct)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("W1:DevelopmentOnly")) return NotFound();
        try
        {
            var app = services.GetRequiredService<W1DevelopmentApplication>();
            var db = services.GetRequiredService<AmharcDbContext>();
            return Ok(await app.PrepareAsync(db, localId, resolution, ct));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or System.IO.IOException)
        { return Conflict(new { code = e.Message }); }
    }
    [HttpPost("activate/{subject}/{operationKey}")]
    public IActionResult Activate(string subject, string operationKey, [FromBody] JsonObject bundle) =>
        Run(app => { app.Activate(subject, bundle, operationKey); return new { standing = "development/conformance-only" }; });
    [HttpPost("closure/{subject}")]
    public IActionResult Closure(string subject, [FromBody] JsonObject bundle) =>
        Run(app => { app.InstallObservationClosure(subject, bundle); return new { installed = true }; });
    [HttpPost("command/{subject}")]
    public IActionResult Command(string subject, [FromBody] W1DevelopmentCommand command) =>
        Run(app => { app.Command(subject, command.Operation, command.OperationKey, command.Seconds,
            command.Period, command.Basis); return new { completed = true }; });
}
public sealed record W1DevelopmentCommand(string Operation, string OperationKey,
    int? Seconds = null, string? Period = null, string Basis = "");