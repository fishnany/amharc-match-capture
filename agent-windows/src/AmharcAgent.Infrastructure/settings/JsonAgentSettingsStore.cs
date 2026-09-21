using System.Text.Json;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Settings;

public sealed class JsonAgentSettingsStore(
    string settingsPath,
    ILogger<JsonAgentSettingsStore> logger)
    : IAgentSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        AgentSettings settings,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = settingsPath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                JsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryPath,
            settingsPath,
            overwrite: true);

        logger.LogInformation(
            "AMHARC settings persisted to {SettingsPath}",
            settingsPath);
    }
}