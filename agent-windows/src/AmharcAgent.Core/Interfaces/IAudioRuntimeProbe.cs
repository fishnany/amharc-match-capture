using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IAudioRuntimeProbe
{
    Task<AudioRuntimeProbeResult> ProbeAsync(
        AudioCredential credential,
        CancellationToken cancellationToken = default);
}
