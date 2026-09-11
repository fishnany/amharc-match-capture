using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Audio;

public sealed class AxisRecordingAudioSourceResolver(
    IAudioCredentialProvider credentialProvider)
    : IRecordingAudioSourceResolver
{
    internal const string PresentationPath =
        "/axis-media/media.amp?videocodec=off&audio=1";

    private readonly IAudioCredentialProvider _credentialProvider =
        credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));

    public async ValueTask<RecordingAudioSourceResolution> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var profile = FieldNetworkProfile.Canonical;
        var credentialResolution =
            await _credentialProvider.ResolveAsync(cancellationToken);

        return credentialResolution.Availability switch
        {
            AudioCredentialAvailability.Available
                when credentialResolution.Credential is not null =>
                RecordingAudioSourceResolution.Available(
                    profile.AudioAddress,
                    profile.AudioRtspPort,
                    PresentationPath,
                    credentialResolution.Credential),

            AudioCredentialAvailability.Missing =>
                RecordingAudioSourceResolution.MissingCredential(
                    profile.AudioAddress,
                    profile.AudioRtspPort,
                    PresentationPath),

            _ =>
                RecordingAudioSourceResolution.Unavailable(
                    profile.AudioAddress,
                    profile.AudioRtspPort,
                    PresentationPath)
        };
    }
}