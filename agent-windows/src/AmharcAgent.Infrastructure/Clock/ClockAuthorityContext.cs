using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>
/// Process-lifetime Capture clock authority state.
///
/// A new Capture Agent process establishes a new authority instance.
/// Sequence numbers are monotonic within that authority incarnation.
/// </summary>
public sealed class ClockAuthorityContext : IClockAuthorityContext
{
    private long _sequence;

    public ClockAuthorityContext()
        : this(
            instanceId: Guid.NewGuid().ToString("D"),
            authorityEpoch: 0)
    {
    }

    internal ClockAuthorityContext(
        string instanceId,
        long authorityEpoch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        if (authorityEpoch < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authorityEpoch));
        }

        Authority =
            new ClockAuthorityV1(
                SourceApplication: "amharc-match-capture",
                InstanceId: instanceId);

        AuthorityEpoch =
            authorityEpoch;
    }

    public ClockAuthorityV1 Authority { get; }

    public long AuthorityEpoch { get; }

    public long NextSequence() =>
        Interlocked.Increment(ref _sequence);
}
