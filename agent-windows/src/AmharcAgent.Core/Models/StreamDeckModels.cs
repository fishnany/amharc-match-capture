namespace AmharcAgent.Core.Models;

/// <summary>Configuration for a single Stream Deck button.</summary>
public record StreamDeckButtonConfig(
    int ButtonNumber,
    string Label,
    string? Icon,
    string? Colour,
    string EventType,
    string? Team,
    string? ScoreEffect,
    string? OverlayEffect,
    bool ClipRequest,
    bool Enabled);

/// <summary>A named button layout profile for a specific sport.</summary>
public record StreamDeckProfile(
    string ProfileId,
    string Name,
    string Sport,
    IReadOnlyList<StreamDeckButtonConfig> Buttons);

/// <summary>Event args for a Stream Deck button press.</summary>
public sealed class StreamDeckButtonPressedEventArgs : EventArgs
{
    public int ButtonNumber { get; init; }
    public StreamDeckButtonConfig? Config { get; init; }
}
