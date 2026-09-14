using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Models;

/// <summary>
/// Runtime-only health snapshot for canonical live-video media ingress.
/// Contains no RTSP URI or credential material.
/// </summary>
public sealed record StreamReceiverHealth(
    StreamReceiverState State,
    string CameraId,
    double? BitRate,
    double? FrameRate,
    int? DroppedFrames,
    DateTimeOffset Timestamp);
