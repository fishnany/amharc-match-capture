using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Overlay;
using FluentAssertions;
using Moq;
using Xunit;

using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class BroadcastPresentationStateServiceTests
{
    private static ScoreState Score(
        string matchId = "match-1") =>
        new(
            MatchId: matchId,
            Sport: Sport.GaelicFootball,
            ScoringModel: ScoringModel.GoalsTwoPointOnePoint,
            HomeGoals: 1,
            HomeTwoPointScores: 2,
            HomePoints: 5,
            AwayGoals: 0,
            AwayTwoPointScores: 1,
            AwayPoints: 8,
            UpdatedAt:
                new DateTimeOffset(
                    2026,
                    9,
                    8,
                    10,
                    0,
                    0,
                    TimeSpan.Zero));

    private static ClockSnapshotV1 Clock(
        string matchId = "match-1") =>
        new(
            ContractVersion: "1.0",
            MatchId: matchId,
            Period: 1,
            PeriodClockSeconds: 100,
            TotalMatchElapsedSeconds: 100,
            IsRunning: true,
            RecordingElapsedSeconds: 120,
            ObservedAtUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    8,
                    10,
                    0,
                    1,
                    TimeSpan.Zero),
            Authority:
                new ClockAuthorityV1(
                    "amharc-match-capture",
                    "capture-instance-1"),
            AuthorityEpoch: 1,
            Sequence: 10);

    private static OverlayState Overlay() =>
        new(
            ActiveTemplateId: "standard-scoreboard",
            IsVisible: true,
            OutputMode: OverlayOutputMode.Programme,
            CurrentGraphic: null,
            GraphicVisible: false,
            HomeGoals: 99,
            HomePoints: 98,
            AwayGoals: 97,
            AwayPoints: 96,
            MatchClockSeconds: 9999,
            CurrentPeriod: 9);

    [Fact]
    public async Task CreateStateAsync_ComposesCanonicalServices()
    {
        var match =
            new DomainMatch
            {
                MatchId = "match-1",
                Sport = Sport.GaelicFootball,
                Competition = "All-Ireland Senior Football Championship",
                Season = "2026",
                Round = "Semi-Final",
                HomeTeam = "Kildare",
                AwayTeam = "Galway",
                Venue = "Croke Park",
                Date = new DateOnly(2026, 8, 9)
            };

        var matches =
            new Mock<IMatchRepository>();

        matches
            .Setup(r => r.GetByIdAsync(
                "match-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        var scoring =
            new Mock<IScoringService>();

        var score =
            Score();

        scoring
            .Setup(s => s.GetState(match))
            .Returns(score);

        var clock =
            new Mock<ICanonicalClockSnapshotService>();

        var snapshot =
            Clock();

        clock
            .Setup(c => c.CreateSnapshot("match-1"))
            .Returns(snapshot);

        var overlay =
            new Mock<IOverlayService>();

        var overlayState =
            Overlay();

        overlay
            .SetupGet(o => o.State)
            .Returns(overlayState);

        var sut =
            new BroadcastPresentationStateService(
                matches.Object,
                scoring.Object,
                clock.Object,
                overlay.Object);

        var result =
            await sut.CreateStateAsync(
                "match-1");

        result.MatchId
            .Should()
            .Be("match-1");

        result.Match.HomeTeam
            .Should()
            .Be("Kildare");

        result.Match.AwayTeam
            .Should()
            .Be("Galway");

        result.Match.Competition
            .Should()
            .Be("All-Ireland Senior Football Championship");

        result.Match.Season
            .Should()
            .Be("2026");

        result.Match.Round
            .Should()
            .Be("Semi-Final");

        result.Match.Venue
            .Should()
            .Be("Croke Park");

        result.Match.Date
            .Should()
            .Be(new DateOnly(2026, 8, 9));

        result.Score
            .Should()
            .BeSameAs(score);

        result.Clock
            .Should()
            .BeSameAs(snapshot);

        result.Presentation.OutputMode
            .Should()
            .Be(OverlayOutputMode.Programme);

        matches.Verify(
            r => r.GetByIdAsync(
                "match-1",
                It.IsAny<CancellationToken>()),
            Times.Once);

        scoring.Verify(
            s => s.GetState(match),
            Times.Once);

        clock.Verify(
            c => c.CreateSnapshot("match-1"),
            Times.Once);

        overlay.VerifyGet(
            o => o.State,
            Times.Once);
    }

    [Fact]
    public async Task CreateStateAsync_RejectsMissingMatchBeforeComposingState()
    {
        var matches =
            new Mock<IMatchRepository>();

        matches
            .Setup(r => r.GetByIdAsync(
                "missing-match",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainMatch?)null);

        var scoring =
            new Mock<IScoringService>();

        var clock =
            new Mock<ICanonicalClockSnapshotService>();

        var overlay =
            new Mock<IOverlayService>();

        var sut =
            new BroadcastPresentationStateService(
                matches.Object,
                scoring.Object,
                clock.Object,
                overlay.Object);

        var action =
            () => sut.CreateStateAsync(
                "missing-match");

        await action
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing-match*does not exist*");

        scoring.Verify(
            s => s.GetState(
                It.IsAny<DomainMatch>()),
            Times.Never);

        clock.Verify(
            c => c.CreateSnapshot(
                It.IsAny<string>()),
            Times.Never);

        overlay.VerifyGet(
            o => o.State,
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task CreateStateAsync_RejectsBlankMatchIdBeforeRepositoryAccess(
        string matchId)
    {
        var matches =
            new Mock<IMatchRepository>();

        var scoring =
            new Mock<IScoringService>();

        var clock =
            new Mock<ICanonicalClockSnapshotService>();

        var overlay =
            new Mock<IOverlayService>();

        var sut =
            new BroadcastPresentationStateService(
                matches.Object,
                scoring.Object,
                clock.Object,
                overlay.Object);

        var action =
            () => sut.CreateStateAsync(
                matchId);

        await action
            .Should()
            .ThrowAsync<ArgumentException>();

        matches.Verify(
            r => r.GetByIdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        scoring.Verify(
            s => s.GetState(
                It.IsAny<DomainMatch>()),
            Times.Never);

        clock.Verify(
            c => c.CreateSnapshot(
                It.IsAny<string>()),
            Times.Never);

        overlay.VerifyGet(
            o => o.State,
            Times.Never);
    }
}
