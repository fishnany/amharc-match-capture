using System.Diagnostics;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Clock;

/// <summary>
/// Implements the dual-clock model.
///
/// INVARIANT: MatchClockSeconds and RecordingElapsedSeconds are ALWAYS independent.
/// - Match clock: pauses during half-time, can be corrected by operator.
/// - Recording clock: starts with recording, NEVER pauses, not affected by corrections.
/// </summary>
public class MatchClockService : IMatchClockService, IDisposable
{
    private readonly IMatchClockStateStore _stateStore;
    private readonly ILogger<MatchClockService> _logger;

    private readonly Stopwatch _matchStopwatch = new();
    private readonly Stopwatch _recordingStopwatch = new();

    private readonly System.Threading.Timer _timer;
    private readonly object _lock = new();

    private readonly List<ClockCorrectionEntry> _auditLog = new();

    private double _matchOffsetSeconds;
    private double _recordingOffsetSeconds;

    private bool _isRunning;
    private int _currentPeriod;

    public event Action<ClockState>? StateChanged;

    public MatchClockService(
        IMatchClockStateStore stateStore,
        ILogger<MatchClockService> logger)
    {
        _stateStore = stateStore;
        _logger = logger;

        _timer = new System.Threading.Timer(
            _ => FireIfRunning(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    public ClockState State => BuildState();

    public void Start()
    {
        lock (_lock)
        {
            _matchStopwatch.Restart();
            _recordingStopwatch.Restart();

            _matchOffsetSeconds = 0;
            _recordingOffsetSeconds = 0;

            _isRunning = true;
            _currentPeriod = 1;

            _timer.Change(
                500,
                500);
        }

        _logger.LogInformation(
            "Match clock started — Period 1");

        StateChanged?.Invoke(State);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            // Accumulate official match time before pausing.
            _matchOffsetSeconds +=
                _matchStopwatch.Elapsed.TotalSeconds;

            _matchStopwatch.Reset();

            _isRunning = false;

            // Recording stopwatch deliberately continues.
        }

        _logger.LogInformation(
            "Match clock paused at {Secs}s",
            (int)_matchOffsetSeconds);

        StateChanged?.Invoke(State);
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            _matchStopwatch.Restart();
            _isRunning = true;

            _timer.Change(
                500,
                500);
        }

        _logger.LogInformation(
            "Match clock resumed");

        StateChanged?.Invoke(State);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _matchStopwatch.Reset();
            _recordingStopwatch.Reset();

            _matchOffsetSeconds = 0;
            _recordingOffsetSeconds = 0;

            _isRunning = false;
            _currentPeriod = 0;

            _timer.Change(
                Timeout.Infinite,
                Timeout.Infinite);
        }

        StateChanged?.Invoke(State);
    }

    public void Correct(
        int matchClockSeconds,
        string? reason)
    {
        lock (_lock)
        {
            var previous =
                (int)GetMatchClockSeconds();

            // Adjust the offset so GetMatchClockSeconds()
            // immediately returns matchClockSeconds.
            _matchOffsetSeconds =
                matchClockSeconds -
                _matchStopwatch.Elapsed.TotalSeconds;

            _auditLog.Add(
                new ClockCorrectionEntry(
                    DateTimeOffset.UtcNow,
                    previous,
                    matchClockSeconds,
                    reason,
                    null));

            _logger.LogInformation(
                "Clock corrected: {Old}s → {New}s. Reason: {Reason}",
                previous,
                matchClockSeconds,
                reason ?? "none");

            // RecordingElapsedSeconds is deliberately untouched.
        }

        StateChanged?.Invoke(State);
    }

    public void StartPeriod(int period)
    {
        lock (_lock)
        {
            _currentPeriod = period;
        }

        _logger.LogInformation(
            "Period {Period} started",
            period);

        StateChanged?.Invoke(State);
    }

    public void EndPeriod(int period)
    {
        _logger.LogInformation(
            "Period {Period} ended",
            period);

        StateChanged?.Invoke(State);
    }

    public void StartHalfTime()
    {
        Pause();

        _logger.LogInformation(
            "Half-time");

        StateChanged?.Invoke(State);
    }

    public void EndHalfTime()
    {
        _logger.LogInformation(
            "Half-time ended");

        StateChanged?.Invoke(State);
    }

    public void MarkFullTime()
    {
        Pause();

        _logger.LogInformation(
            "Full time");

        StateChanged?.Invoke(State);
    }

    public async Task SaveRuntimeStateAsync(
        string matchId,
        CancellationToken ct = default)
    {
        ClockState state;

        lock (_lock)
        {
            state = BuildState();
        }

        var now =
            DateTimeOffset.UtcNow;

        var runtimeState =
            new MatchClockRuntimeState
            {
                MatchId = matchId,
                MatchClockSeconds =
                    state.MatchClockSeconds,
                RecordingElapsedSeconds =
                    state.RecordingElapsedSeconds,
                IsRunning =
                    state.IsRunning,
                CurrentPeriod =
                    state.CurrentPeriod,
                ClockMode =
                    state.ClockMode,
                PersistedAt =
                    now,
                UpdatedAt =
                    now
            };

        await _stateStore.SaveAsync(
            runtimeState,
            ct);

        _logger.LogInformation(
            "Match clock runtime state persisted for match {MatchId}: match={MatchSecs}s, recording={RecordingSecs}s, running={IsRunning}",
            matchId,
            runtimeState.MatchClockSeconds,
            runtimeState.RecordingElapsedSeconds,
            runtimeState.IsRunning);
    }

    public async Task<bool> RecoverRuntimeStateAsync(
        string matchId,
        CancellationToken ct = default)
    {
        var persisted =
            await _stateStore.LoadAsync(
                matchId,
                ct);

        if (persisted is null)
        {
            _logger.LogInformation(
                "No persisted match clock runtime state found for match {MatchId}",
                matchId);

            return false;
        }

        var now =
            DateTimeOffset.UtcNow;

        var elapsedSincePersist =
            Math.Max(
                0,
                (now - persisted.PersistedAt)
                    .TotalSeconds);

        lock (_lock)
        {
            _matchStopwatch.Reset();
            _recordingStopwatch.Reset();

            _matchOffsetSeconds =
                persisted.MatchClockSeconds;

            if (persisted.IsRunning)
            {
                _matchOffsetSeconds +=
                    elapsedSincePersist;

                _matchStopwatch.Restart();
                _isRunning = true;
            }
            else
            {
                _isRunning = false;
            }

            _recordingOffsetSeconds =
                persisted.RecordingElapsedSeconds +
                elapsedSincePersist;

            // Under the existing dual-clock contract, recording elapsed
            // time continues independently of match-clock pause state.
            _recordingStopwatch.Restart();

            _currentPeriod =
                persisted.CurrentPeriod;

            _timer.Change(
                _isRunning
                    ? 500
                    : Timeout.Infinite,
                _isRunning
                    ? 500
                    : Timeout.Infinite);
        }

        _logger.LogInformation(
            "Recovered match clock runtime state for match {MatchId}: match={MatchSecs}s, recording={RecordingSecs}s, running={IsRunning}",
            matchId,
            State.MatchClockSeconds,
            State.RecordingElapsedSeconds,
            State.IsRunning);

        StateChanged?.Invoke(State);

        return true;
    }

    public IReadOnlyList<ClockCorrectionEntry>
        GetAuditLog() =>
        _auditLog.AsReadOnly();

    private double GetMatchClockSeconds() =>
        _matchOffsetSeconds +
        (_isRunning
            ? _matchStopwatch.Elapsed.TotalSeconds
            : 0);

    private ClockState BuildState() =>
        new(
            MatchClockSeconds:
                (int)GetMatchClockSeconds(),

            RecordingElapsedSeconds:
                (int)(
                    _recordingOffsetSeconds +
                    _recordingStopwatch.Elapsed.TotalSeconds),

            IsRunning:
                _isRunning,

            CurrentPeriod:
                _currentPeriod,

            ClockMode:
                "count-up",

            UpdatedAt:
                DateTimeOffset.UtcNow);

    private void FireIfRunning()
    {
        if (_isRunning)
        {
            StateChanged?.Invoke(State);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();

        _matchStopwatch.Stop();
        _recordingStopwatch.Stop();
    }
}