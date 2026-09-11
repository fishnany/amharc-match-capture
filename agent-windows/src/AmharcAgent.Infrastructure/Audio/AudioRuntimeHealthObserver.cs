using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Audio;

/// <summary>
/// Maps one bounded credential/probe observation into the cached audio
/// runtime health authority. Scheduling is introduced only after this
/// deterministic iteration seam is fully tested.
/// </summary>
internal sealed class AudioRuntimeHealthObserver(
    IAudioCredentialProvider credentials,
    IAudioRuntimeProbe probe,
    IAudioRuntimeHealthPublisher publisher)
    : IAudioRuntimeHealthObserver
{
    public async Task ObserveOnceAsync(
        CancellationToken cancellationToken = default)
    {
        var resolution =
            await credentials.ResolveAsync(cancellationToken);

        if (resolution.Availability != AudioCredentialAvailability.Available ||
            resolution.Credential is null)
        {
            publisher.Publish(
                new AudioRuntimeHealthState(
                    AudioRuntimeHealthStatus.Blocked,
                    FieldNetworkProfile.Canonical.AudioAddress,
                    FieldNetworkProfile.Canonical.AudioRtspPort,
                    false, false, false, false,
                    null, null, null, null,
                    DateTimeOffset.UtcNow,
                    resolution.Detail));
            return;
        }

        var result =
            await probe.ProbeAsync(
                resolution.Credential,
                cancellationToken);

        var status =
            result.IsReady
                ? AudioRuntimeHealthStatus.Ready
                : result.Failure == AudioRuntimeProbeFailure.AuthenticationRejected
                    ? AudioRuntimeHealthStatus.Blocked
                    : AudioRuntimeHealthStatus.Degraded;

        publisher.Publish(
            new AudioRuntimeHealthState(
                status,
                FieldNetworkProfile.Canonical.AudioAddress,
                FieldNetworkProfile.Canonical.AudioRtspPort,
                result.EndpointReachable,
                result.Authenticated,
                result.AudioAdvertised,
                result.MediaFlowObserved,
                result.Codec,
                result.SampleRateHz,
                result.Channels,
                result.PayloadType,
                DateTimeOffset.UtcNow,
                result.Detail));
    }
}
