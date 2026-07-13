using AmharcAgent.Infrastructure.Clock;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AmharcAgent.Tests;

/// <summary>
/// CRITICAL: MatchClockSeconds and RecordingElapsedSeconds MUST always be independent.
/// These tests enforce the dual-clock invariant from ADR-004.
/// </summary>
public class MatchClockServiceTests : IDisposable
{
    private readonly MatchClockService _sut = new(NullLogger<MatchClockService>.Instance);

    [Fact]
    public void Start_SetsIsRunning_True()
    {
        _sut.Start();
        _sut.State.IsRunning.Should().BeTrue();
        _sut.State.CurrentPeriod.Should().Be(1);
    }

    [Fact]
    public void Pause_StopsMatchClock_ButRecordingClockContinues()
    {
        _sut.Start();
        Thread.Sleep(200); // let both clocks tick
        _sut.Pause();

        var stateAfterPause = _sut.State;
        stateAfterPause.IsRunning.Should().BeFalse();
        stateAfterPause.MatchClockSeconds.Should().BeGreaterOrEqualTo(0);

        Thread.Sleep(200); // recording elapsed should continue increasing
        var stateAfterWait = _sut.State;

        // KEY INVARIANT: match clock is frozen, recording clock continues
        stateAfterWait.MatchClockSeconds.Should().Be(stateAfterPause.MatchClockSeconds,
            "match clock must be frozen after pause");
        stateAfterWait.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(stateAfterPause.RecordingElapsedSeconds,
            "recording elapsed must NEVER pause");
    }

    [Fact]
    public void Correct_ChangesMatchClock_ButNeverRecordingClock()
    {
        _sut.Start();
        Thread.Sleep(100);
        var recBefore = _sut.State.RecordingElapsedSeconds;

        _sut.Correct(600, "Operator correction — 10 minutes");
        var state = _sut.State;

        state.MatchClockSeconds.Should().Be(600);
        // Recording elapsed must not have been reset or altered
        state.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(recBefore,
            "correction must NEVER touch recording elapsed seconds");
    }

    [Fact]
    public void AuditLog_RecordsCorrection()
    {
        _sut.Start();
        _sut.Correct(300, "test correction");
        _sut.GetAuditLog().Should().ContainSingle(e => e.NewMatchClockSeconds == 300);
    }

    [Fact]
    public void Reset_ClearsBothClocks()
    {
        _sut.Start();
        Thread.Sleep(100);
        _sut.Reset();
        _sut.State.MatchClockSeconds.Should().Be(0);
        _sut.State.RecordingElapsedSeconds.Should().Be(0);
        _sut.State.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Resume_AfterPause_ContinuesMatchClockFromPausePoint()
    {
        _sut.Start();
        Thread.Sleep(100);
        _sut.Pause();
        var pausedAt = _sut.State.MatchClockSeconds;

        Thread.Sleep(100);
        _sut.Resume();
        Thread.Sleep(100);

        _sut.State.MatchClockSeconds.Should().BeGreaterThan(pausedAt,
            "match clock should resume from where it paused");
    }

    public void Dispose() => _sut.Dispose();
}
