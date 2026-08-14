using AmharcAgent.Api.Hubs;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data;
using AmharcAgent.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddAmharcInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AMHARC Match Capture API", Version = "v1" });
});

var app = builder.Build();

// ── DB: apply migrations ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AmharcDbContext>();
    db.Database.Migrate();
    Log.Information("Database ready: {Db}", db.Database.GetConnectionString());
}

// ── Background services: start hardware listeners ────────────────────────────
var settings = app.Services.GetRequiredService<AmharcAgent.Core.Domain.AgentSettings>();
if (settings.StreamDeckEnabled)
{
    var streamDeckOwnership =
        app.Services.GetRequiredService<IStreamDeckOwnershipService>();

    var ownershipState =
        await streamDeckOwnership.AcquireAsync(
            app.Lifetime.ApplicationStopping);

    if (ownershipState ==
        AmharcAgent.Core.Models.StreamDeckOwnershipState.Controlled)
{
    var streamDeck =
        app.Services.GetRequiredService<IStreamDeckService>();

    if (settings.StreamDeck.RestoreActiveProfileOnStartup &&
        !string.IsNullOrWhiteSpace(
            settings.StreamDeck.ActiveProfileId))
    {
        using var profileScope =
            app.Services.CreateScope();

        var profileDb =
            profileScope.ServiceProvider
                .GetRequiredService<AmharcDbContext>();

        var profile =
            await profileDb.StreamDeckProfiles.FindAsync(
                [
                    settings.StreamDeck.ActiveProfileId
                ],
                app.Lifetime.ApplicationStopping);

        if (profile is not null)
        {
            await streamDeck.LoadProfileAsync(
                profile,
                app.Lifetime.ApplicationStopping);

            Log.Information(
                "Restored Stream Deck profile {ProfileName} ({ProfileId})",
                profile.Name,
                profile.ProfileId);
        }
        else
        {
            Log.Warning(
                "Configured Stream Deck profile {ProfileId} could not be found",
                settings.StreamDeck.ActiveProfileId);
        }
    }

    var streamDeckCommandBridge =
        app.Services.GetRequiredService<
            AmharcAgent.Infrastructure.StreamDeck.StreamDeckCommandBridge>();

    streamDeckCommandBridge.Start();

    _ = streamDeck.StartAsync(
        app.Lifetime.ApplicationStopping);
}
    else
    {
        Log.Warning(
            "Stream Deck startup skipped because ownership state is {OwnershipState}",
            ownershipState);
    }
}
if (settings.JoystickEnabled)
{
    var joystick = app.Services.GetRequiredService<IJoystickService>();

    var joystickPtzBridge =
        app.Services.GetRequiredService<
            AmharcAgent.Infrastructure.Joystick.JoystickPtzBridge>();

    joystickPtzBridge.Start();

    _ = joystick.StartAsync(app.Lifetime.ApplicationStopping);
}

// ── Pipeline ─────────────────────────────────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the operator UI static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<MatchHub>("/hubs/match");

// SPA fallback: all non-API, non-file routes return index.html
app.MapFallbackToFile("index.html");

Log.Information("AMHARC Agent starting on {Urls}", builder.Configuration["Urls"]);
app.Run();
