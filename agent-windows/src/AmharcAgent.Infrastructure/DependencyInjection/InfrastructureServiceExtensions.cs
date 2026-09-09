using System.Text.Json;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Camera;
using AmharcAgent.Infrastructure.Clock;
using AmharcAgent.Infrastructure.Commands;
using AmharcAgent.Infrastructure.Events;
using AmharcAgent.Infrastructure.Export;
using AmharcAgent.Infrastructure.Health;
using AmharcAgent.Infrastructure.Joystick;
using AmharcAgent.Infrastructure.Overlay;
using AmharcAgent.Infrastructure.Recording;
using AmharcAgent.Infrastructure.Readiness;
using AmharcAgent.Infrastructure.Runtime;
using AmharcAgent.Infrastructure.Scoring;
using AmharcAgent.Infrastructure.Settings;
using AmharcAgent.Infrastructure.Storage;
using AmharcAgent.Infrastructure.StreamDeck;
using AmharcAgent.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace AmharcAgent.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddAmharcInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
var settings = configuration
    .GetSection("AmharcAgent")
    .Get<AgentSettings>()
    ?? new AgentSettings();

var settingsDirectory = Path.Combine(
    Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData),
    "AMHARC",
    "MatchCapture");

var persistedSettingsPath = Path.Combine(
    settingsDirectory,
    "agent-settings.json");

if (File.Exists(persistedSettingsPath))
{
    try
    {
        var json = File.ReadAllText(persistedSettingsPath);

        var persistedSettings =
            JsonSerializer.Deserialize<AgentSettings>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (persistedSettings is not null)
        {
            settings = persistedSettings;
        }
    }
    catch
    {
        // Invalid persisted settings fall back to appsettings.json defaults.
        // Runtime logging can be added here in a later refinement.
    }
}

services.AddSingleton(settings);


var resolvedFfmpegPath =
    FfmpegRuntimeResolver.Resolve(
        settings.FfmpegPath);

settings.FfmpegPath =
    resolvedFfmpegPath;
services.AddSingleton<IAgentSettingsStore>(sp =>
    new JsonAgentSettingsStore(
        persistedSettingsPath,
        sp.GetRequiredService<
            Microsoft.Extensions.Logging.ILogger<
                JsonAgentSettingsStore>>()));

        // â”€â”€ Database â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddDbContextFactory<AmharcDbContext>(opts =>
            opts.UseSqlite(
                configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=amharc.db"));

        services.AddScoped<AmharcDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AmharcDbContext>>()
                .CreateDbContext());

        // â”€â”€ Repositories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ICameraRepository, CameraRepository>();
        services.AddScoped<IEventRepository, EventRepository>();

        // â”€â”€ Camera â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Use a placeholder camera; the real one is configured after first-run setup
        var placeholderCamera = new AmharcAgent.Core.Domain.Camera
        {
            CameraId = "primary",
            Name = "Primary Camera",
            Manufacturer = "AXIS",
            Model = "Q6128-E",
            IpAddress = "192.168.1.135",
            Username = settings.DefaultCameraUsername,
            Password = settings.DefaultCameraPassword
        };
        services.AddSingleton<ICameraDiscoveryService, CameraDiscoveryService>();
        services.AddSingleton<AxisCameraAdapter>(sp =>
            new AxisCameraAdapter(placeholderCamera,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AxisCameraAdapter>>()));
        services.AddSingleton<ICameraAdapter>(sp => sp.GetRequiredService<AxisCameraAdapter>());
        services.AddSingleton<IPtzController>(sp => sp.GetRequiredService<AxisCameraAdapter>());

        // â”€â”€ Recording & Streaming â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddSingleton<IRecordingService>(sp =>
            new FfmpegRecordingService(
                sp.GetRequiredService<ILogger<FfmpegRecordingService>>(),
                sp.GetRequiredService<IRecordingSessionStore>(),
                sp.GetRequiredService<ICameraAdapter>(),
                settings.FfmpegPath));

        services.AddSingleton<IStreamingService>(sp =>
            new RtmpStreamingService(
                sp.GetRequiredService<ILogger<RtmpStreamingService>>(),
                settings.FfmpegPath));

        // â”€â”€ Stream Deck & Joystick â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Stream Deck
        services.AddSingleton<AmharcStreamDeckButtonRenderer>();
        services.AddSingleton<IStreamDeckProcessManager, StreamDeckProcessManager>();
        services.AddSingleton<IStreamDeckOwnershipService, StreamDeckOwnershipService>();
        services.AddSingleton<IStreamDeckService, StreamDeckService>();
        services.AddSingleton<StreamDeckCommandBridge>();
        services.AddSingleton<IJoystickService>(sp => new JoystickService(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JoystickService>>(), settings.Joystick));
        services.AddSingleton<JoystickPtzBridge>();

        // â”€â”€ Clock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddSingleton<IMatchClockStateStore, MatchClockStateStore>();
        services.AddSingleton<IMatchClockService, MatchClockService>();
        services.AddSingleton<IClockAuthorityContext, ClockAuthorityContext>();
        services.AddSingleton<ICanonicalClockSnapshotService, CanonicalClockSnapshotService>();
        services.AddSingleton<IRecordingSessionStore, RecordingSessionStore>();

        // â”€â”€ Commands, Events, Scoring, Storage, Overlay â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddScoped<IAmharcCommandDispatcher, AmharcCommandDispatcher>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<IEventTaggingService, EventTaggingService>();
        services.AddSingleton<IStorageMonitorService>(sp =>
            new StorageMonitorService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StorageMonitorService>>(),
                settings.RecordingDirectory));
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddScoped<IBroadcastPresentationStateService, BroadcastPresentationStateService>();

        // â”€â”€ Health â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddSingleton<IHealthMonitoringService, HealthMonitoringService>();
        services.AddScoped<ILiveReadinessService, LiveReadinessService>();

        // â”€â”€ Export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        services.AddScoped<IExportService, ExportService>();

        return services;
    }
}
