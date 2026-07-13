using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>
/// Manages the dual-clock model:
/// MatchClockSeconds = official match time (pauseable, correctable).
/// RecordingElapsedSeconds = continuous wall time since recording started (NEVER paused).
/// These two values are ALWAYS independent.
/// </summary>
public interface IMatchClockService
{
    ClockState State { get; }

    void Start();
    void Pause();
    void Resume();
    void Reset();
    void Correct(int matchClockSeconds, string? reason);
    void StartPeriod(int period);
    void EndPeriod(int period);
    void StartHalfTime();
    void EndHalfTime();
    void MarkFullTime();

    IReadOnlyList<ClockCorrectionEntry> GetAuditLog();
    event Action<ClockState> StateChanged;
}
