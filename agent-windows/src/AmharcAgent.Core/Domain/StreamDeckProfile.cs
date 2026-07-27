namespace AmharcAgent.Core.Domain;

/// <summary>Which team a Stream Deck button is associated with.</summary>
public enum ButtonTeam { Home, Away }

/// <summary>Configuration for a single Stream Deck button (0-indexed, 0-14 for 15-key XL).</summary>
public class StreamDeckButton
{
    public int ButtonNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>Background colour in hex (e.g. "#1C8551").</summary>
    public string? Colour { get; set; }

    /// <summary>Event type to tag when pressed (e.g. "point", "goal").</summary>
    public string EventType { get; set; } = string.Empty;

    public ButtonTeam? Team { get; set; }
    /// <summary>Optional score effect: goal, point, or two-point. Two-point is valid only for men's Gaelic football.</summary>
    public string? ScoreEffect { get; set; }
    public string? OverlayEffect { get; set; }
    public bool ClipRequest { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>A named Stream Deck button layout for a specific sport.</summary>
public class StreamDeckProfile
{
    public string ProfileId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public List<StreamDeckButton> Buttons { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
