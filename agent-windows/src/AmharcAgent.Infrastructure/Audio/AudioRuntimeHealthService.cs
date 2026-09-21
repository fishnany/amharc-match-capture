using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Audio;

public sealed class AudioRuntimeHealthService :
    IAudioRuntimeHealthService,
    IAudioRuntimeHealthPublisher
{
    private readonly object _sync = new();
    private AudioRuntimeHealthState _current =
        AudioRuntimeHealthState.Unknown(DateTimeOffset.MinValue);

    public AudioRuntimeHealthState Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Publish(AudioRuntimeHealthState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_sync)
        {
            if (state.ObservedAtUtc < _current.ObservedAtUtc)
                return;
            _current = state;
        }
    }
}
