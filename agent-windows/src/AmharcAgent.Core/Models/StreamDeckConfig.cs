namespace AmharcAgent.Core.Models;

/// <summary>
/// Governs AMHARC ownership and startup behaviour for Stream Deck hardware.
/// </summary>
public record StreamDeckConfig
{
    public bool ExclusiveOwnership { get; init; } = true;
    public bool CloseCompetingSoftwareOnStartup { get; init; } = true;
    public bool ClearDeviceOnStartup { get; init; } = true;
    public bool RestoreProfileOnReconnect { get; init; } = true;
    public bool RestoreActiveProfileOnStartup { get; init; } = true;
    public string? ActiveProfileId { get; init; }
}
