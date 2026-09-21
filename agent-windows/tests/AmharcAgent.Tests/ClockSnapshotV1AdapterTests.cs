using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Models;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class ClockSnapshotV1AdapterTests
{
    private static readonly ClockAuthorityV1 Authority =
        new(
            SourceApplication: "capture",
            InstanceId: "capture-instance-1");

    [Fact]
    public void FromClockState_PeriodOne_DerivesPeriodClockFromZeroBoundary()
    {
        var observedAt =
            DateTimeOffset.Parse("2026-09-07T14:42:18.421Z");

        var state =
            new ClockState(
                MatchClockSeconds: 125,
                RecordingElapsedSeconds: 150,
                IsRunning: true,
                CurrentPeriod: 1,
                PeriodStartTotalMatchElapsedSeconds: 0,
                ClockMode: "count-up",
                UpdatedAt: observedAt);

        var snapshot =
            ClockSnapshotV1Adapter.FromClockState(
                state,
                "match-1",
                Authority,
                authorityEpoch: 1,
                sequence: 7);

        snapshot.ContractVersion.Should().Be("1.0");
        snapshot.MatchId.Should().Be("match-1");
        snapshot.Period.Should().Be(1);
        snapshot.TotalMatchElapsedSeconds.Should().Be(125);
        snapshot.PeriodClockSeconds.Should().Be(125);
    }

    [Fact]
    public void FromClockState_LaterPeriod_DerivesClockFromPersistedBoundary()
    {
        var state =
            new ClockState(
                MatchClockSeconds: 2345,
                RecordingElapsedSeconds: 2700,
                IsRunning: true,
                CurrentPeriod: 2,
                PeriodStartTotalMatchElapsedSeconds: 2100,
                ClockMode: "count-up",
                UpdatedAt: DateTimeOffset.UtcNow);

        var snapshot =
            ClockSnapshotV1Adapter.FromClockState(
                state,
                "match-1",
                Authority,
                authorityEpoch: 1,
                sequence: 8);

        snapshot.TotalMatchElapsedSeconds.Should().Be(2345);
        snapshot.PeriodClockSeconds.Should().Be(245);
    }

    [Fact]
    public void FromClockState_PreservesRecordingAndObservationState()
    {
        var observedAt =
            DateTimeOffset.Parse("2026-09-07T14:45:00Z");

        var state =
            new ClockState(
                MatchClockSeconds: 500,
                RecordingElapsedSeconds: 725,
                IsRunning: false,
                CurrentPeriod: 1,
                PeriodStartTotalMatchElapsedSeconds: 0,
                ClockMode: "count-up",
                UpdatedAt: observedAt);

        var snapshot =
            ClockSnapshotV1Adapter.FromClockState(
                state,
                "match-1",
                Authority,
                authorityEpoch: 1,
                sequence: 9);

        snapshot.IsRunning.Should().BeFalse();
        snapshot.RecordingElapsedSeconds.Should().Be(725);
        snapshot.ObservedAtUtc.Should().Be(observedAt);
    }

    [Fact]
    public void FromClockState_PreservesAuthorityEpochAndSequence()
    {
        var state =
            new ClockState(
                MatchClockSeconds: 10,
                RecordingElapsedSeconds: 12,
                IsRunning: true,
                CurrentPeriod: 1,
                PeriodStartTotalMatchElapsedSeconds: 0,
                ClockMode: "count-up",
                UpdatedAt: DateTimeOffset.UtcNow);

        var snapshot =
            ClockSnapshotV1Adapter.FromClockState(
                state,
                "match-1",
                Authority,
                authorityEpoch: 4,
                sequence: 123);

        snapshot.Authority.Should().Be(Authority);
        snapshot.Authority.SourceApplication.Should().Be("capture");
        snapshot.Authority.InstanceId.Should().Be("capture-instance-1");
        snapshot.AuthorityEpoch.Should().Be(4);
        snapshot.Sequence.Should().Be(123);
    }

    [Fact]
    public void FromClockState_LaterPeriodWithUnknownBoundary_Throws()
    {
        var state =
            new ClockState(
                MatchClockSeconds: 2345,
                RecordingElapsedSeconds: 2700,
                IsRunning: false,
                CurrentPeriod: 2,
                PeriodStartTotalMatchElapsedSeconds: null,
                ClockMode: "count-up",
                UpdatedAt: DateTimeOffset.UtcNow);

        var act =
            () => ClockSnapshotV1Adapter.FromClockState(
                state,
                "match-1",
                Authority,
                authorityEpoch: 1,
                sequence: 1);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*current period boundary is unknown*");
    }
}
