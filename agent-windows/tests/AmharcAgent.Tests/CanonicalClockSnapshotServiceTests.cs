using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Clock;
using FluentAssertions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class CanonicalClockSnapshotServiceTests
{
    [Fact]
    public void CreateSnapshot_CombinesClockStateAndAuthorityContext()
    {
        var observedAt =
            DateTimeOffset.Parse("2026-09-07T14:42:18.421Z");

        var state =
            new ClockState(
                MatchClockSeconds: 2274,
                RecordingElapsedSeconds: 2659,
                IsRunning: true,
                CurrentPeriod: 2,
                PeriodStartTotalMatchElapsedSeconds: 2140,
                ClockMode: "count-up",
                UpdatedAt: observedAt);

        var clock =
            new Mock<IMatchClockService>();

        clock
            .SetupGet(c => c.State)
            .Returns(state);

        var authority =
            new Mock<IClockAuthorityContext>();

        authority
            .SetupGet(a => a.Authority)
            .Returns(
                new ClockAuthorityV1(
                    SourceApplication: "amharc-match-capture",
                    InstanceId: "capture-instance-1"));

        authority
            .SetupGet(a => a.AuthorityEpoch)
            .Returns(3);

        authority
            .Setup(a => a.NextSequence())
            .Returns(42);

        var sut =
            new CanonicalClockSnapshotService(
                clock.Object,
                authority.Object);

        var snapshot =
            sut.CreateSnapshot("match-1");

        snapshot.ContractVersion.Should().Be("1.0");
        snapshot.MatchId.Should().Be("match-1");
        snapshot.Period.Should().Be(2);
        snapshot.TotalMatchElapsedSeconds.Should().Be(2274);
        snapshot.PeriodClockSeconds.Should().Be(134);
        snapshot.RecordingElapsedSeconds.Should().Be(2659);
        snapshot.IsRunning.Should().BeTrue();
        snapshot.ObservedAtUtc.Should().Be(observedAt);

        snapshot.Authority.SourceApplication
            .Should()
            .Be("amharc-match-capture");

        snapshot.Authority.InstanceId
            .Should()
            .Be("capture-instance-1");

        snapshot.AuthorityEpoch.Should().Be(3);
        snapshot.Sequence.Should().Be(42);
    }

    [Fact]
    public void CreateSnapshot_RequestsNextSequenceForEachSnapshot()
    {
        var clock =
            new Mock<IMatchClockService>();

        clock
            .SetupGet(c => c.State)
            .Returns(
                new ClockState(
                    MatchClockSeconds: 100,
                    RecordingElapsedSeconds: 120,
                    IsRunning: true,
                    CurrentPeriod: 1,
                    PeriodStartTotalMatchElapsedSeconds: 0,
                    ClockMode: "count-up",
                    UpdatedAt: DateTimeOffset.UtcNow));

        var authority =
            new Mock<IClockAuthorityContext>();

        authority
            .SetupGet(a => a.Authority)
            .Returns(
                new ClockAuthorityV1(
                    "amharc-match-capture",
                    "capture-instance-1"));

        authority
            .SetupGet(a => a.AuthorityEpoch)
            .Returns(0);

        authority
            .SetupSequence(a => a.NextSequence())
            .Returns(1)
            .Returns(2);

        var sut =
            new CanonicalClockSnapshotService(
                clock.Object,
                authority.Object);

        var first =
            sut.CreateSnapshot("match-1");

        var second =
            sut.CreateSnapshot("match-1");

        first.Sequence.Should().Be(1);
        second.Sequence.Should().Be(2);

        authority.Verify(
            a => a.NextSequence(),
            Times.Exactly(2));
    }
}
