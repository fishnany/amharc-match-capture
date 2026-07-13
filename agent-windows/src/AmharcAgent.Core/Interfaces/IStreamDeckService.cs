using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Controls the Elgato Stream Deck hardware device via HID.</summary>
public interface IStreamDeckService
{
    bool IsConnected { get; }
    string? DeviceName { get; }
    string? ActiveProfileId { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task LoadProfileAsync(StreamDeckProfile profile, CancellationToken ct = default);
    Task SetButtonStateAsync(int buttonNumber, bool active, CancellationToken ct = default);
    Task SetButtonLabelAsync(int buttonNumber, string label, CancellationToken ct = default);

    event Action<int, StreamDeckButton> ButtonPressed;
    event Action<string> Connected;
    event Action Disconnected;
}
