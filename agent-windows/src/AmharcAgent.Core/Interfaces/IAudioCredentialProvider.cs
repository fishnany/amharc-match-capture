namespace AmharcAgent.Core.Interfaces;

using AmharcAgent.Core.Models;

public interface IAudioCredentialProvider
{
    ValueTask<AudioCredentialResolution> ResolveAsync(CancellationToken cancellationToken = default);
}
