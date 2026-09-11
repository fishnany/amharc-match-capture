namespace AmharcAgent.Core.Models;

public enum AudioRuntimeProbeFailure
{
    None,
    EndpointUnreachable,
    AuthenticationRejected,
    InvalidAudioPresentation,
    MediaSessionFailed,
    MediaFlowNotObserved,
    Cancelled,
    Unexpected
}

public sealed record AudioRuntimeProbeResult(
    bool EndpointReachable,
    bool Authenticated,
    bool AudioAdvertised,
    bool MediaFlowObserved,
    string? Codec,
    int? SampleRateHz,
    int? Channels,
    int? PayloadType,
    AudioRuntimeProbeFailure Failure,
    string? Detail)
{
    public bool IsReady =>
        Failure == AudioRuntimeProbeFailure.None &&
        EndpointReachable &&
        Authenticated &&
        AudioAdvertised &&
        MediaFlowObserved &&
        !string.IsNullOrWhiteSpace(Codec) &&
        SampleRateHz is > 0 &&
        Channels is > 0 &&
        PayloadType is >= 0 and <= 127;
}
