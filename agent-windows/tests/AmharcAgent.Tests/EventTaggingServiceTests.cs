using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Events;
using AmharcAgent.Infrastructure.Scoring;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class EventTaggingServiceTests
{
    private static DomainMatch MakeMatch(
        Sport sport = Sport.GaelicFootball) => new()
        {
            MatchId = "m1",
            Sport = sport,
            HomeTeam = "Clare",
            AwayTeam = "Galway",
            HomeGoals = 0,
            HomeTwoPointScores = 0,
            HomePoints = 0,
            AwayGoals = 0,
            AwayTwoPointScores = 0,
            AwayPoints = 0
        };

    private static EventTaggingService CreateSut(
        Mock<IEventRepository> events,
        Mock<IMatchRepository> matches)
    {
        return new EventTaggingService(
            events.Object,
            matches.Object,
            new ScoringService(),
            NullLogger<EventTaggingService>.Instance);
    }

    private static void ConfigureEventCreate(
        Mock<IEventRepository> events)
    {
        events.Setup(e => e.CreateAsync(
                It.IsAny<MatchEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (MatchEvent ev, CancellationToken _) =>
                    Task.FromResult(ev));
    }

    [Fact]
    public async Task CreateEvent_Point_Home_IncrementsHomePoints()
    {
        var match = MakeMatch();

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        matches.Setup(m => m.GetByIdAsync("m1", default))
            .ReturnsAsync(match);

        matches.Setup(m => m.UpdateAsync(
                It.IsAny<DomainMatch>(),
                default))
            .ReturnsAsync(match);

        ConfigureEventCreate(events);

        var sut = CreateSut(events, matches);

        var opts = new CreateEventOptions(
            "m1",
            "point",
            EventTeam.Home,
            null,
            1,
            120,
            125,
            EventSource.OperatorUi);

        var result = await sut.CreateEventAsync(opts, default);

        result.EventType.Should().Be("point");
        result.Team.Should().Be(EventTeam.Home);

        match.HomePoints.Should().Be(1);
        match.HomeGoals.Should().Be(0);
        match.HomeTwoPointScores.Should().Be(0);

        result.ScoreBeforeState.Should().NotBeNull();
        result.ScoreAfterState.Should().NotBeNull();

        result.ScoreBeforeState!.Home.OnePointScores
            .Should().Be(0);

        result.ScoreAfterState!.Home.OnePointScores
            .Should().Be(1);

        result.MatchClockSeconds.Should().Be(120);

        result.RecordingElapsedSeconds.Should().Be(
            125,
            "RecordingElapsedSeconds must be stored independently from MatchClockSeconds");
    }

    [Fact]
    public async Task CreateEvent_DualClockValues_StoredIndependently()
    {
        var match = MakeMatch();

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        matches.Setup(m => m.GetByIdAsync("m1", default))
            .ReturnsAsync(match);

        matches.Setup(m => m.UpdateAsync(
                It.IsAny<DomainMatch>(),
                default))
            .ReturnsAsync(match);

        ConfigureEventCreate(events);

        var sut = CreateSut(events, matches);

        var opts = new CreateEventOptions(
            "m1",
            "goal",
            EventTeam.Away,
            null,
            2,
            600,
            650,
            EventSource.StreamDeck);

        var result = await sut.CreateEventAsync(opts, default);

        result.MatchClockSeconds.Should().Be(600);
        result.RecordingElapsedSeconds.Should().Be(650);

        result.MatchClockSeconds.Should().NotBe(
            result.RecordingElapsedSeconds,
            "dual-clock values should differ when non-playing recording time has elapsed");

        match.AwayGoals.Should().Be(1);
    }

    [Fact]
    public async Task CreateEvent_TwoPoint_Home_UsesMensFootballScoring()
    {
        var match = MakeMatch(Sport.GaelicFootball);

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        matches.Setup(m => m.GetByIdAsync("m1", default))
            .ReturnsAsync(match);

        matches.Setup(m => m.UpdateAsync(
                It.IsAny<DomainMatch>(),
                default))
            .ReturnsAsync(match);

        ConfigureEventCreate(events);

        var sut = CreateSut(events, matches);

        var opts = new CreateEventOptions(
            "m1",
            "two-point-score",
            EventTeam.Home,
            null,
            1,
            200,
            205,
            EventSource.StreamDeck);

        var result = await sut.CreateEventAsync(opts, default);

        match.HomeTwoPointScores.Should().Be(1);
        match.HomeTotal.Should().Be(2);

        result.ScoreBeforeState.Should().NotBeNull();
        result.ScoreAfterState.Should().NotBeNull();

        result.ScoreBeforeState!.Home.TwoPointScores
            .Should().Be(0);

        result.ScoreAfterState!.Home.TwoPointScores
            .Should().Be(1);

        result.ScoreAfterState.ScoringModel
            .Should().Be(
                ScoringModel.GoalsTwoPointOnePoint);
    }

    [Fact]
    public async Task CreateEvent_TwoPoint_Lgfa_IsRejected()
    {
        var match = MakeMatch(Sport.LadiesFootball);

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        matches.Setup(m => m.GetByIdAsync("m1", default))
            .ReturnsAsync(match);

        var sut = CreateSut(events, matches);

        var opts = new CreateEventOptions(
            "m1",
            "two-point-score",
            EventTeam.Home,
            null,
            1,
            100,
            100,
            EventSource.OperatorUi);

        var act = async () =>
            await sut.CreateEventAsync(opts, default);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "*only for men's Gaelic football*");

        match.HomeTwoPointScores.Should().Be(0);

        events.Verify(
            e => e.CreateAsync(
                It.IsAny<MatchEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UndoLastEvent_RestoresCanonicalScoreBeforeState()
    {
        var match = MakeMatch(Sport.GaelicFootball);

        match.HomeGoals = 1;
        match.HomeTwoPointScores = 2;
        match.HomePoints = 5;

        var before = new ScoreSnapshot(
            ScoringModel.GoalsTwoPointOnePoint,
            new TeamScoreState(1, 1, 5),
            new TeamScoreState(0, 2, 4));

        var after = new ScoreSnapshot(
            ScoringModel.GoalsTwoPointOnePoint,
            new TeamScoreState(1, 2, 5),
            new TeamScoreState(0, 2, 4));

        var lastEvent = new MatchEvent
        {
            EventId = "e-last",
            MatchId = "m1",
            EventType = "two-point-score",
            Team = EventTeam.Home,
            ScoreBeforeState = before,
            ScoreAfterState = after,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        events.Setup(e => e.GetLastEventAsync("m1", default))
            .ReturnsAsync(lastEvent);

        matches.Setup(m => m.GetByIdAsync("m1", default))
            .ReturnsAsync(match);

        matches.Setup(m => m.UpdateAsync(
                It.IsAny<DomainMatch>(),
                default))
            .ReturnsAsync(match);

        events.Setup(e => e.DeleteAsync(
                "e-last",
                default))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(events, matches);

        var undone = await sut.UndoLastEventAsync(
            "m1",
            default);

        undone.Should().BeSameAs(lastEvent);

        match.HomeGoals.Should().Be(1);
        match.HomeTwoPointScores.Should().Be(1);
        match.HomePoints.Should().Be(5);

        match.AwayGoals.Should().Be(0);
        match.AwayTwoPointScores.Should().Be(2);
        match.AwayPoints.Should().Be(4);

        matches.Verify(
            m => m.UpdateAsync(
                match,
                It.IsAny<CancellationToken>()),
            Times.Once);

        events.Verify(
            e => e.DeleteAsync(
                "e-last",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportEventsCsv_ContainsDualClockColumns()
    {
        var evt = new MatchEvent
        {
            EventId = "e1",
            MatchId = "m1",
            EventType = "point",
            MatchClockSeconds = 300,
            RecordingElapsedSeconds = 340,
            Period = 1,
            Source = EventSource.OperatorUi,
            SystemTimestamp = DateTimeOffset.UtcNow
        };

        var events = new Mock<IEventRepository>();
        var matches = new Mock<IMatchRepository>();

        events.Setup(e => e.GetByMatchIdAsync(
                "m1",
                default))
            .ReturnsAsync(
                new List<MatchEvent> { evt });

        var sut = CreateSut(events, matches);

        var csv = await sut.ExportEventsCsvAsync(
            "m1",
            default);

        csv.Should().Contain("MatchClockSeconds");
        csv.Should().Contain("RecordingElapsedSeconds");
        csv.Should().Contain("300");
        csv.Should().Contain("340");
    }
}
