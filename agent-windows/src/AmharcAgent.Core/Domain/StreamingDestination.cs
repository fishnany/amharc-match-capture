namespace AmharcAgent.Core.Domain;

/// <summary>Streaming platform.</summary>
public enum StreamingPlatform { YouTube, Facebook, Twitch, Custom }

/// <summary>Configured RTMP streaming destination.</summary>
public class StreamingDestination
{
    public string DestinationId { get; set; } = Guid.NewGuid().ToString();
    public StreamingPlatform Platform { get; set; } = StreamingPlatform.Custom;
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string StreamKey { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public int? FrameRate { get; set; }
    public int? BitRate { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
