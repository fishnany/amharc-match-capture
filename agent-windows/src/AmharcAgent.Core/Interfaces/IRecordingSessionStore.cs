using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Interfaces;

public interface IRecordingSessionStore
{
    Task<RecordingSession?> GetByIdAsync(
        string recordingId,
        CancellationToken ct = default);

    Task<RecordingSession?> GetActiveForMatchAsync(
        string matchId,
        CancellationToken ct = default);

    Task<RecordingSession?> GetRecoverableAsync(
        CancellationToken ct = default);

    Task SaveAsync(
        RecordingSession session,
        CancellationToken ct = default);
}