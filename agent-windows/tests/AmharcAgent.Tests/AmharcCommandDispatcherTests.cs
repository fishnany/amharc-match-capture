using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Commands;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class AmharcCommandDispatcherTests
{
    private static DomainMatch ActiveMatch() => new()
    {
        MatchId = "m1",
        Sport = Sport.GaelicFootball,
        HomeTeam = "Home",
        AwayTeam = "Away",
        Status = MatchStatus.Active,
        CurrentPeriod = 1
    };

    private static ClockState Clock() => new(
        MatchClockSeconds: 321,
        RecordingElapsedSeconds: 345,
        IsRunning: true,
        CurrentPeriod: 1,
        ClockMode: "match",
        UpdatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task ScoreHomeTwoPoint_CreatesCanonicalStreamDeckEvent()
    {
        var match = ActiveMatch();

        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches.Setup(m => m.GetActiveMatchAsync(default))
            .ReturnsAsync(match);

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        CreateEventOptions? captured = null;

        events.Setup(e => e.CreateEventAsync(
                It.IsAny<CreateEventOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateEventOptions, CancellationToken>(
                (opts, _) => captured = opts)
            .ReturnsAsync(new MatchEvent());

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.ScoreHomeTwoPoint,
                EventSource.StreamDeck));

        captured.Should().NotBeNull();
        captured!.MatchId.Should().Be("m1");
        captured.EventType.Should().Be("two-point-score");
        captured.Team.Should().Be(EventTeam.Home);
        captured.Source.Should().Be(EventSource.StreamDeck);
        captured.MatchClockSeconds.Should().Be(321);
        captured.RecordingElapsedSeconds.Should().Be(345);
        captured.Period.Should().Be(1);
    }

    [Fact]
    public async Task ExplicitMatchId_DoesNotRequireActiveMatchLookup()
    {
        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        events.Setup(e => e.CreateEventAsync(
                It.IsAny<CreateEventOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MatchEvent());

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.ScoreAwayGoal,
                EventSource.Api,
                MatchId: "explicit-match"));

        matches.Verify(
            m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        events.Verify(
            e => e.CreateEventAsync(
                It.Is<CreateEventOptions>(
                    o => o.MatchId == "explicit-match" &&
                         o.EventType == "goal" &&
                         o.Team == EventTeam.Away),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoActiveMatch_ThrowsForScoreCommand()
    {
        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches.Setup(m => m.GetActiveMatchAsync(default))
            .ReturnsAsync((DomainMatch?)null);

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        var act = async () =>
            await sut.DispatchAsync(
                new AmharcCommand(
                    AmharcCommandIds.ScoreHomeGoal,
                    EventSource.StreamDeck));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active AMHARC match*");

        events.Verify(
            e => e.CreateEventAsync(
                It.IsAny<CreateEventOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MatchClockStart_StartsClock_AndPersistsRuntimeState()
    {
        var match = ActiveMatch();

        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        clock
            .Setup(c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockStart,
                EventSource.OperatorUi));

        clock.Verify(
            c => c.Start(),
            Times.Once);

        clock.Verify(
            c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MatchClockPause_PausesClock_AndPersistsRuntimeState()
    {
        var match = ActiveMatch();

        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        clock
            .Setup(c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockPause,
                EventSource.OperatorUi));

        clock.Verify(
            c => c.Pause(),
            Times.Once);

        clock.Verify(
            c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MatchClockResume_ResumesClock_AndPersistsRuntimeState()
    {
        var match = ActiveMatch();

        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        clock
            .Setup(c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.MatchClockResume,
                EventSource.OperatorUi));

        clock.Verify(
            c => c.Resume(),
            Times.Once);

        clock.Verify(
            c => c.SaveRuntimeStateAsync(
                "m1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EventUndo_UsesActiveMatch()
    {
        var match = ActiveMatch();

        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        matches.Setup(m => m.GetActiveMatchAsync(default))
            .ReturnsAsync(match);

        events.Setup(e => e.UndoLastEventAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MatchEvent());

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        await sut.DispatchAsync(
            new AmharcCommand(
                AmharcCommandIds.EventUndo,
                EventSource.StreamDeck));

        events.Verify(
            e => e.UndoLastEventAsync(
                "m1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnsupportedCommand_Throws()
    {
        var matches = new Mock<IMatchRepository>();
        var events = new Mock<IEventTaggingService>();
        var clock = new Mock<IMatchClockService>();

        var sut = new AmharcCommandDispatcher(
            matches.Object,
            events.Object,
            clock.Object,
            NullLogger<AmharcCommandDispatcher>.Instance);

        var act = async () =>
            await sut.DispatchAsync(
                new AmharcCommand(
                    "unsupported.command",
                    EventSource.Api));

        await act.Should()
            .ThrowAsync<NotSupportedException>();
    }
}
