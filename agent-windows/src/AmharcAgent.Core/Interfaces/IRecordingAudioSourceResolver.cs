using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IRecordingAudioSourceResolver
{
    ValueTask<RecordingAudioSourceResolution> ResolveAsync(
        CancellationToken cancellationToken = default);
}