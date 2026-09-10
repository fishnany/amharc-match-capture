using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IAudioRuntimeHealthService
{
    AudioRuntimeHealthState Current { get; }
}
