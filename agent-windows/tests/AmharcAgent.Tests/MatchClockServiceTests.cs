using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.Clock;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

/// <summary>
/// CRITICAL: MatchClockSeconds and RecordingElapsedSeconds MUST always be independent.
/// These tests enforce the dual-clock invariant from ADR-004.
/// </summary>
public class MatchClockServiceTests : IDisposable
{
    private readonly Mock<IMatchClockStateStore> _stateStore = new();
    private readonly MatchClockService _sut;

    public MatchClockServiceTests()
    {
        _sut = new MatchClockService(
            _stateStore.Object,
            NullLogger<MatchClockService>.Instance);
    }

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

        Thread.Sleep(200);

        _sut.Pause();

        var stateAfterPause = _sut.State;

        stateAfterPause.IsRunning.Should().BeFalse();
        stateAfterPause.MatchClockSeconds.Should().BeGreaterOrEqualTo(0);

        Thread.Sleep(200);

        var stateAfterWait = _sut.State;

        stateAfterWait.MatchClockSeconds.Should().Be(
            stateAfterPause.MatchClockSeconds,
            "match clock must be frozen after pause");

        stateAfterWait.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(
            stateAfterPause.RecordingElapsedSeconds,
            "recording elapsed must NEVER pause");
    }

    [Fact]
    public void Correct_ChangesMatchClock_ButNeverRecordingClock()
    {
        _sut.Start();

        Thread.Sleep(100);

        var recBefore =
            _sut.State.RecordingElapsedSeconds;

        _sut.Correct(
            600,
            "Operator correction — 10 minutes");

        var state = _sut.State;

        state.MatchClockSeconds.Should().Be(600);

        state.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(
            recBefore,
            "correction must NEVER touch recording elapsed seconds");
    }

    [Fact]
    public void AuditLog_RecordsCorrection()
    {
        _sut.Start();

        _sut.Correct(
            300,
            "test correction");

        _sut.GetAuditLog()
            .Should()
            .ContainSingle(
                e => e.NewMatchClockSeconds == 300);
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

        var pausedAt =
            _sut.State.MatchClockSeconds;

        Thread.Sleep(100);

        _sut.Resume();

        var advanced =
            SpinWait.SpinUntil(
                () =>
                    _sut.State.MatchClockSeconds >
                    pausedAt,
                TimeSpan.FromSeconds(2));

        advanced.Should().BeTrue(
            "the match clock should resume from its paused position and advance");
    }

    [Fact]
    public async Task SaveRuntimeStateAsync_PersistsCurrentClockState()
    {
        MatchClockRuntimeState? captured = null;

        _stateStore
            .Setup(s => s.SaveAsync(
                It.IsAny<MatchClockRuntimeState>(),
                It.IsAny<CancellationToken>()))
            .Callback<MatchClockRuntimeState, CancellationToken>(
                (state, _) => captured = state)
            .Returns(Task.CompletedTask);

        _sut.Start();

        _sut.Correct(
            120,
            "test");

        await _sut.SaveRuntimeStateAsync(
            "match-1");

        captured.Should().NotBeNull();

        captured!.MatchId.Should().Be("match-1");
        captured.MatchClockSeconds.Should().Be(120);
        captured.IsRunning.Should().BeTrue();
        captured.CurrentPeriod.Should().Be(1);
        captured.ClockMode.Should().Be("count-up");
    }

    [Fact]
    public async Task RecoverRuntimeStateAsync_NoPersistedState_ReturnsFalse()
    {
        _stateStore
            .Setup(s => s.LoadAsync(
                "match-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (MatchClockRuntimeState?)null);

        var recovered =
            await _sut.RecoverRuntimeStateAsync(
                "match-1");

        recovered.Should().BeFalse();
    }

    [Fact]
    public async Task RecoverRuntimeStateAsync_PausedState_RestoresFrozenMatchClock()
    {
        var persistedAt =
            DateTimeOffset.UtcNow;

        _stateStore
            .Setup(s => s.LoadAsync(
                "match-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MatchClockRuntimeState
                {
                    MatchId = "match-1",
                    MatchClockSeconds = 300,
                    RecordingElapsedSeconds = 400,
                    IsRunning = false,
                    CurrentPeriod = 1,
                    ClockMode = "count-up",
                    PersistedAt = persistedAt
                });

        var recovered =
            await _sut.RecoverRuntimeStateAsync(
                "match-1");

        recovered.Should().BeTrue();

        _sut.State.IsRunning.Should().BeFalse();
        _sut.State.MatchClockSeconds.Should().Be(300);
        _sut.State.CurrentPeriod.Should().Be(1);
        _sut.State.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(400);
    }

    [Fact]
    public async Task RecoverRuntimeStateAsync_RunningState_AdvancesFromPersistedAnchor()
    {
        var persistedAt =
            DateTimeOffset.UtcNow.AddSeconds(-2);

        _stateStore
            .Setup(s => s.LoadAsync(
                "match-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MatchClockRuntimeState
                {
                    MatchId = "match-1",
                    MatchClockSeconds = 300,
                    RecordingElapsedSeconds = 400,
                    IsRunning = true,
                    CurrentPeriod = 2,
                    ClockMode = "count-up",
                    PersistedAt = persistedAt
                });

        var recovered =
            await _sut.RecoverRuntimeStateAsync(
                "match-1");

        recovered.Should().BeTrue();

        _sut.State.IsRunning.Should().BeTrue();
        _sut.State.MatchClockSeconds.Should().BeGreaterOrEqualTo(302);
        _sut.State.RecordingElapsedSeconds.Should().BeGreaterOrEqualTo(402);
        _sut.State.CurrentPeriod.Should().Be(2);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}