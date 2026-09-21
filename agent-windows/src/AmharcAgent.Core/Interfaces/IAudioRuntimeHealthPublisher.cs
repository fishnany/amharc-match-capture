using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IAudioRuntimeHealthPublisher
{
    void Publish(AudioRuntimeHealthState state);
}
