using AmharcAgent.Core.Contracts;

namespace AmharcAgent.Core.Interfaces;

public interface IClockAuthorityContext
{
    ClockAuthorityV1 Authority { get; }

    long AuthorityEpoch { get; }

    long NextSequence();
}
