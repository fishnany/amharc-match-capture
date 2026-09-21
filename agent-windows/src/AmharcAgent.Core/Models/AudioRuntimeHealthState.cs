namespace AmharcAgent.Core.Models;

public enum AudioRuntimeHealthStatus
{
    Unknown,
    Ready,
    Degraded,
    Blocked
}

public sealed record AudioRuntimeHealthState(
    AudioRuntimeHealthStatus Status,
    string? Endpoint,
    int? Port,
    bool EndpointReachable,
    bool Authenticated,
    bool AudioAdvertised,
    bool MediaFlowObserved,
    string? Codec,
    int? SampleRateHz,
    int? Channels,
    int? PayloadType,
    DateTimeOffset ObservedAtUtc,
    string? Detail = null)
{
    public bool IsReady => Status == AudioRuntimeHealthStatus.Ready;

    public static AudioRuntimeHealthState Unknown(
        DateTimeOffset? observedAtUtc = null,
        string? detail = null) =>
        new(
            AudioRuntimeHealthStatus.Unknown, null, null,
            false, false, false, false,
            null, null, null, null,
            observedAtUtc ?? DateTimeOffset.UtcNow,
            detail ?? "Audio runtime health has not yet been observed.");

    public static AudioRuntimeHealthState Create(
        string endpoint,
        int port,
        bool endpointReachable,
        bool authenticated,
        bool audioAdvertised,
        bool mediaFlowObserved,
        string? codec,
        int? sampleRateHz,
        int? channels,
        int? payloadType,
        DateTimeOffset? observedAtUtc = null,
        string? detail = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Audio endpoint is required.", nameof(endpoint));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        var metadataValid =
            !string.IsNullOrWhiteSpace(codec) &&
            sampleRateHz.HasValue && sampleRateHz.Value > 0 &&
            channels.HasValue && channels.Value > 0 &&
            payloadType.HasValue &&
            payloadType.Value >= 0 && payloadType.Value <= 127;

        var status =
            !endpointReachable
                ? AudioRuntimeHealthStatus.Blocked
                : authenticated && audioAdvertised &&
                  mediaFlowObserved && metadataValid
                    ? AudioRuntimeHealthStatus.Ready
                    : AudioRuntimeHealthStatus.Degraded;

        return new AudioRuntimeHealthState(
            status, endpoint.Trim(), port,
            endpointReachable, authenticated,
            audioAdvertised, mediaFlowObserved,
            codec == null ? null : codec.Trim(),
            sampleRateHz, channels, payloadType,
            observedAtUtc ?? DateTimeOffset.UtcNow,
            detail);
    }
}
