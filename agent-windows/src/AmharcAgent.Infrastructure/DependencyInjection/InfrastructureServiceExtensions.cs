using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Camera;
using AmharcAgent.Infrastructure.Clock;
using AmharcAgent.Infrastructure.Events;
using AmharcAgent.Infrastructure.Export;
using AmharcAgent.Infrastructure.Health;
using AmharcAgent.Infrastructure.Joystick;
using AmharcAgent.Infrastructure.Overlay;
using AmharcAgent.Infrastructure.Recording;
using AmharcAgent.Infrastructure.Storage;
using AmharcAgent.Infrastructure.StreamDeck;
using AmharcAgent.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AmharcAgent.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddAmharcInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Settings ─────────────────────────────────────────────────────────
        var settings = configuration.GetSection("AmharcAgent").Get<AgentSettings>()
            ?? new AgentSettings();
        services.AddSingleton(settings);

        // ── Database ─────────────────────────────────────────────────────────
        services.AddDbContext<AmharcDbContext>(opts =>
            opts.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=amharc.db"));

        // ── Repositories ─────────────────────────────────────────────────────
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ICameraRepository, CameraRepository>();
        services.AddScoped<IEventRepository, EventRepository>();

        // ── Camera ───────────────────────────────────────────────────────────
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

        // ── Recording & Streaming ─────────────────────────────────────────────
        services.AddSingleton<IRecordingService>(sp =>
            new FfmpegRecordingService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FfmpegRecordingService>>(),
                settings.FfmpegPath));
        services.AddSingleton<IStreamingService>(sp =>
            new RtmpStreamingService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RtmpStreamingService>>(),
                settings.FfmpegPath));

        // ── Stream Deck & Joystick ────────────────────────────────────────────
        services.AddSingleton<IStreamDeckService, StreamDeckService>();
        services.AddSingleton<IJoystickService>(sp =>
            new JoystickService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JoystickService>>(),
                new JoystickConfig()));

        // ── Clock ─────────────────────────────────────────────────────────────
        services.AddSingleton<IMatchClockService, MatchClockService>();

        // ── Events, Storage, Overlay ──────────────────────────────────────────
        services.AddScoped<IEventTaggingService, EventTaggingService>();
        services.AddSingleton<IStorageMonitorService>(sp =>
            new StorageMonitorService(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StorageMonitorService>>(),
                settings.RecordingDirectory));
        services.AddSingleton<IOverlayService, OverlayService>();

        // ── Health ────────────────────────────────────────────────────────────
        services.AddSingleton<IHealthMonitoringService, HealthMonitoringService>();

        // ── Export ────────────────────────────────────────────────────────────
        services.AddScoped<IExportService, ExportService>();

        return services;
    }
}
