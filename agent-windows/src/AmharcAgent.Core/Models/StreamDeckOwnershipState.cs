namespace AmharcAgent.Core.Models;

/// <summary>
/// Runtime ownership state of the Stream Deck from the AMHARC Agent perspective.
/// </summary>
public enum StreamDeckOwnershipState
{
    Unavailable,
    Connecting,
    Conflicted,
    Controlled,
    Error
}
