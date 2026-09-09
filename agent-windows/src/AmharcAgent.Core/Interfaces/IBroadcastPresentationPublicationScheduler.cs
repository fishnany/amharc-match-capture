namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Requests publication of the complete canonical broadcast presentation state.
///
/// Callers identify only which match has changed. They must not provide score,
/// clock or presentation fragments. The publication layer re-composes
/// BroadcastPresentationStateV1 from authoritative domain services.
/// </summary>
public interface IBroadcastPresentationPublicationScheduler
{
    void RequestPublication(string matchId);
}
