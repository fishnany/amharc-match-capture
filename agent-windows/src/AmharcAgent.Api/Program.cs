using System.Text.Json;
using System.Text.Json.Serialization;
using AmharcAgent.Api.Hubs;
using AmharcAgent.Api.Publication;
using AmharcAgent.Api.Runtime;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Exceptions;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data;
using AmharcAgent.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// â”€â”€ Serilog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddAmharcInfrastructure(builder.Configuration);
builder.Services.AddHostedService<StreamReceiverLifecycleHostedService>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<
    IClockSnapshotPublisher,
    SignalRClockSnapshotPublisher>();
builder.Services.AddScoped<
    IBroadcastPresentationPublisher,
    SignalRBroadcastPresentationPublisher>();
builder.Services.AddSingleton<BroadcastPresentationPublicationHostedService>();
builder.Services.AddSingleton<IBroadcastPresentationPublicationScheduler>(
    sp => sp.GetRequiredService<BroadcastPresentationPublicationHostedService>());
builder.Services.AddSingleton<IHostedService>(
    sp => sp.GetRequiredService<BroadcastPresentationPublicationHostedService>());
builder.Services.AddSingleton<ClockSnapshotPublicationHostedService>();
builder.Services.AddSingleton<IClockSnapshotPublicationScheduler>(
    sp => sp.GetRequiredService<ClockSnapshotPublicationHostedService>());
builder.Services.AddSingleton<IHostedService>(
    sp => sp.GetRequiredService<ClockSnapshotPublicationHostedService>());
builder.Services.AddSingleton<AudioRuntimeHealthHostedService>();
builder.Services.AddSingleton<IHostedService>(
    sp => sp.GetRequiredService<AudioRuntimeHealthHostedService>());
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AMHARC Match Capture API", Version = "v1" });
});

var app = builder.Build();

// â”€â”€ DB: apply migrations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AmharcDbContext>();
    db.Database.Migrate();
    Log.Information("Database ready: {Db}", db.Database.GetConnectionString());
}

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var cameraRepository =
        startupScope.ServiceProvider.GetRequiredService<
            AmharcAgent.Data.Repositories.ICameraRepository>();

    var primaryCamera =
        await cameraRepository.GetByIdAsync(
            "primary");

    if (primaryCamera is null)
    {
        var cameraSettings =
            startupScope.ServiceProvider.GetRequiredService<
                AmharcAgent.Core.Domain.AgentSettings>();

        var protectedCredentials =
            startupScope.ServiceProvider.GetRequiredService<
                IProtectedCredentialStore>();

        if (!string.IsNullOrEmpty(cameraSettings.DefaultCameraPassword))
        {
            await protectedCredentials.WriteAsync(
                "AMHARC/Camera/primary",
                new AmharcAgent.Core.Models.ProtectedCredential(
                    cameraSettings.DefaultCameraUsername,
                    cameraSettings.DefaultCameraPassword));
        }

        await cameraRepository.CreateAsync(
            new AmharcAgent.Core.Domain.Camera
            {
                CameraId = "primary",
                Name = "Primary Camera",
                Manufacturer = "AXIS",
                Model = "Q6128-E",
                IpAddress = "192.168.1.135",
                RtspPort = 554,
                HttpPort = 80,
                Username = string.Empty,
                Password = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
    }
}

// â”€â”€ Live match recovery â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
using (var recoveryScope = app.Services.CreateScope())
{
    var matchRepository =
        recoveryScope.ServiceProvider
            .GetRequiredService<AmharcAgent.Data.Repositories.IMatchRepository>();

    var clock =
        app.Services.GetRequiredService<IMatchClockService>();

    try
    {
        var activeMatch =
            await matchRepository.GetActiveMatchAsync(
                app.Lifetime.ApplicationStopping);

        if (activeMatch is null)
        {
            Log.Information(
                "No active or half-time match found; startup recovery not required");
        }
        else
        {
            Log.Information(
                "Recovering live match {MatchId} with status {Status}",
                activeMatch.MatchId,
                activeMatch.Status);

            var recovered =
                await clock.RecoverRuntimeStateAsync(
                    activeMatch.MatchId,
                    app.Lifetime.ApplicationStopping);

            if (recovered)
            {
                Log.Information(
                    "Live match {MatchId} recovered successfully: period={Period}, match={MatchSeconds}s, recording={RecordingSeconds}s, running={IsRunning}",
                    activeMatch.MatchId,
                    clock.State.CurrentPeriod,
                    clock.State.MatchClockSeconds,
                    clock.State.RecordingElapsedSeconds,
                    clock.State.IsRunning);
            }
            else
            {
                Log.Warning(
                    "Match {MatchId} is marked {Status}, but no persisted clock runtime state was available; automatic clock recovery was not performed",
                    activeMatch.MatchId,
                    activeMatch.Status);
            }
        }
    }
    catch (OperationCanceledException)
        when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        Log.Information(
            "Live match recovery cancelled because the application is stopping");
    }
    catch (Exception ex)
    {
        Log.Error(
            ex,
            "Live match recovery failed during Agent startup");

        throw;
    }
}

// â”€â”€ Recording recovery â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
try
{
    using var recordingRecoveryScope =
        app.Services.CreateScope();

    var recordingMatchRepository =
        recordingRecoveryScope.ServiceProvider
            .GetRequiredService<AmharcAgent.Data.Repositories.IMatchRepository>();

    var recording =
        app.Services.GetRequiredService<IRecordingService>();

    var activeRecordingMatch =
        await recordingMatchRepository.GetActiveMatchAsync(
            app.Lifetime.ApplicationStopping);

    if (activeRecordingMatch is null)
    {
        Log.Information(
            "No active or half-time match found; automatic recording recovery skipped");
    }
    else
    {
        Log.Information(
            "Checking for interrupted recording session for active match {MatchId}",
            activeRecordingMatch.MatchId);

        await recording.RecoverAsync(
            activeRecordingMatch.MatchId,
            app.Lifetime.ApplicationStopping);

        if (recording.State == RecordingState.Recording)
        {
            Log.Information(
                "Interrupted recording recovered successfully for match {MatchId}; output directory={OutputDirectory}, segments={SegmentCount}",
                activeRecordingMatch.MatchId,
                recording.OutputDirectory,
                recording.SegmentCount);
        }
        else
        {
            Log.Information(
                "No interrupted recording required recovery for match {MatchId}; recording state={State}",
                activeRecordingMatch.MatchId,
                recording.State);
        }
    }
}
catch (OperationCanceledException)
    when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
{
    Log.Information(
        "Recording recovery cancelled because the application is stopping");
}
catch (Exception ex)
{
    Log.Error(
        ex,
        "Recording recovery failed during Agent startup");

    throw;
}

// â”€â”€ Background services: start hardware listeners â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

// â”€â”€ Pipeline â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

app.UseSerilogRequestLogging();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (MatchLifecycleConflictException ex)
    {
        Log.Warning(
            ex,
            "Match lifecycle conflict for {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        context.Response.StatusCode =
            StatusCodes.Status409Conflict;

        context.Response.ContentType =
            "application/problem+json";

        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://httpstatuses.com/409",
                title = "Match lifecycle conflict",
                status = StatusCodes.Status409Conflict,
                detail = ex.Message,
                instance = context.Request.Path.Value
            },
            context.RequestAborted);
    }
});

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
