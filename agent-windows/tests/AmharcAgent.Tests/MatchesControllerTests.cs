using AmharcAgent.Api.Controllers;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class MatchesControllerTests
{
    private static DomainMatch Match() => new()
    {
        MatchId = "m1",
        Sport = Sport.GaelicFootball,
        HomeTeam = "Home",
        AwayTeam = "Away",
        Status = MatchStatus.Ready,
        CurrentPeriod = 0
    };

    private static ClockState Clock() => new(
        MatchClockSeconds: 123,
        RecordingElapsedSeconds: 150,
        IsRunning: true,
        CurrentPeriod: 1,
        ClockMode: "count-up",
        UpdatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task StartClock_DispatchesSemanticStartCommand()
    {
        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        AmharcCommand? captured = null;

        dispatcher
            .Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<AmharcCommand, CancellationToken>(
                (command, _) => captured = command)
            .Returns(Task.CompletedTask);

        var sut = new MatchesController(
            repo.Object,
            clock.Object,
            dispatcher.Object,
            overlay.Object,
            NullLogger<MatchesController>.Instance);

        var result =
            await sut.StartClock(
                "m1",
                default);

        result.Should().BeOfType<OkObjectResult>();

        captured.Should().NotBeNull();
        captured!.CommandId.Should()
            .Be(AmharcCommandIds.MatchClockStart);
        captured.MatchId.Should().Be("m1");
        captured.Source.Should().Be(EventSource.Api);

        clock.Verify(
            c => c.Start(),
            Times.Never);
    }

    [Fact]
    public async Task PauseClock_DispatchesSemanticPauseCommand()
    {
        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        await new MatchesController(
                repo.Object,
                clock.Object,
                dispatcher.Object,
                overlay.Object,
                NullLogger<MatchesController>.Instance)
            .PauseClock(
                "m1",
                default);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(
                    c =>
                        c.CommandId ==
                            AmharcCommandIds.MatchClockPause &&
                        c.MatchId == "m1" &&
                        c.Source == EventSource.Api),
                It.IsAny<CancellationToken>()),
            Times.Once);

        clock.Verify(
            c => c.Pause(),
            Times.Never);
    }

    [Fact]
    public async Task ResumeClock_DispatchesSemanticResumeCommand()
    {
        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        await new MatchesController(
                repo.Object,
                clock.Object,
                dispatcher.Object,
                overlay.Object,
                NullLogger<MatchesController>.Instance)
            .ResumeClock(
                "m1",
                default);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(
                    c =>
                        c.CommandId ==
                            AmharcCommandIds.MatchClockResume &&
                        c.MatchId == "m1"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        clock.Verify(
            c => c.Resume(),
            Times.Never);
    }

    [Fact]
    public async Task CorrectClock_DispatchesCorrectionParameters()
    {
        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        clock.SetupGet(c => c.State)
            .Returns(Clock());

        AmharcCommand? captured = null;

        dispatcher
            .Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<AmharcCommand, CancellationToken>(
                (command, _) => captured = command)
            .Returns(Task.CompletedTask);

        var sut = new MatchesController(
            repo.Object,
            clock.Object,
            dispatcher.Object,
            overlay.Object,
            NullLogger<MatchesController>.Instance);

        await sut.CorrectClock(
            "m1",
            new ClockCorrectRequest(
                600,
                "Operator correction"),
            default);

        captured.Should().NotBeNull();

        captured!.CommandId.Should()
            .Be(AmharcCommandIds.MatchClockCorrect);

        captured.MatchId.Should().Be("m1");

        captured.Parameters.Should().NotBeNull();

        captured.Parameters!["matchClockSeconds"]
            .Should().Be("600");

        captured.Parameters["reason"]
            .Should().Be("Operator correction");

        clock.Verify(
            c => c.Correct(
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task StartMatch_ActivatesMatch_AndDispatchesClockStart()
    {
        var match = Match();

        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        repo
            .Setup(r => r.GetByIdAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        repo
            .Setup(r => r.UpdateAsync(
                It.IsAny<DomainMatch>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (DomainMatch m, CancellationToken _) => m);

        var sut = new MatchesController(
            repo.Object,
            clock.Object,
            dispatcher.Object,
            overlay.Object,
            NullLogger<MatchesController>.Instance);

        var result =
            await sut.StartMatch(
                "m1",
                default);

        result.Should().BeOfType<OkObjectResult>();

        match.Status.Should().Be(MatchStatus.Active);
        match.CurrentPeriod.Should().Be(1);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(
                    c =>
                        c.CommandId ==
                            AmharcCommandIds.MatchClockStart &&
                        c.MatchId == "m1"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        clock.Verify(
            c => c.Start(),
            Times.Never);
    }

    [Fact]
    public async Task StopMatch_CompletesMatch_AndDispatchesFullTime()
    {
        var match = Match();
        match.Status = MatchStatus.Active;

        var repo = new Mock<IMatchRepository>();
        var clock = new Mock<IMatchClockService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();
        var overlay = new Mock<IOverlayService>();

        repo
            .Setup(r => r.GetByIdAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        repo
            .Setup(r => r.UpdateAsync(
                It.IsAny<DomainMatch>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (DomainMatch m, CancellationToken _) => m);

        var sut = new MatchesController(
            repo.Object,
            clock.Object,
            dispatcher.Object,
            overlay.Object,
            NullLogger<MatchesController>.Instance);

        var result =
            await sut.StopMatch(
                "m1",
                default);

        result.Should().BeOfType<OkObjectResult>();

        match.Status.Should().Be(MatchStatus.Complete);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(
                    c =>
                        c.CommandId ==
                            AmharcCommandIds.MatchClockFullTime &&
                        c.MatchId == "m1"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        clock.Verify(
            c => c.MarkFullTime(),
            Times.Never);
    }
}