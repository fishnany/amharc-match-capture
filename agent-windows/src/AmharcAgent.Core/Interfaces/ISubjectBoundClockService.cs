using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>All consequential reads and mutations occur under the same clock lock.
/// Subject is established at explicit lifecycle start/recovery, never by a read.</summary>
public interface ISubjectBoundClockService
{
    ValueTask<IAsyncDisposable> EnterCommandAsync(string localMatchId, bool isStart, CancellationToken ct = default);
    void StartFor(string localMatchId);
    void EndSubject(string localMatchId);
    void ApplyFor(string localMatchId, Action action);
    T ReadFor<T>(string localMatchId, Func<ClockState, T> capture);
}