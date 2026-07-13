using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public enum StreamingState { Idle, Connecting, Streaming, Reconnecting, Stopping, Error }

/// <summary>Controls FFmpeg RTMP live streaming.</summary>
public interface IStreamingService
{
    StreamingState State { get; }
    StreamingStats? Stats { get; }

    Task StartAsync(StreamingDestinationConfig destination, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    event Action<StreamingState> StateChanged;
    event Action<Exception> ErrorOccurred;
}

public record StreamingStats(double UptimeSeconds, double OutgoingBitRate, int DroppedFrames, int ReconnectCount);
public record StreamingDestinationConfig(
    string DestinationId, string Platform, string ServerUrl, string StreamKey,
    string? Resolution, int? FrameRate, int? BitRate);
