using AmharcAgent.Api.Hubs;
using AmharcAgent.Api.Publication;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class SignalRBroadcastPresentationPublisherTests
{
    private static BroadcastPresentationStateV1 State() =>
        new(
            ContractVersion:
                BroadcastPresentationStateV1.CurrentContractVersion,
            MatchId:
                "m1",
            Match:
                new BroadcastMatchIdentityV1(
                    Competition: "Test Competition",
                    Season: "2026",
                    Round: "Final",
                    HomeTeam: "HOME",
                    AwayTeam: "AWAY",
                    Venue: "AMHARC Test",
                    Date: new DateOnly(2026, 9, 8)),
            Score:
                new ScoreState(
                    MatchId: "m1",
                    Sport: Sport.GaelicFootball,
                    ScoringModel:
                        ScoringModel.GoalsTwoPointOnePoint,
                    HomeGoals: 1,
                    HomeTwoPointScores: 2,
                    HomePoints: 3,
                    AwayGoals: 0,
                    AwayTwoPointScores: 1,
                    AwayPoints: 4,
                    UpdatedAt:
                        new DateTimeOffset(
                            2026,
                            9,
                            8,
                            12,
                            0,
                            0,
                            TimeSpan.Zero)),
            Clock:
                new ClockSnapshotV1(
                    ContractVersion: "1.0",
                    MatchId: "m1",
                    Period: 1,
                    PeriodClockSeconds: 600,
                    TotalMatchElapsedSeconds: 600,
                    IsRunning: true,
                    RecordingElapsedSeconds: 615,
                    ObservedAtUtc:
                        new DateTimeOffset(
                            2026,
                            9,
                            8,
                            12,
                            10,
                            0,
                            TimeSpan.Zero),
                    Authority:
                        new ClockAuthorityV1(
                            "AMHARC Capture",
                            "capture-instance-1"),
                    AuthorityEpoch: 12,
                    Sequence: 42),
            Presentation:
                new BroadcastPresentationControlV1(
                    ActiveTemplateId: "scoreboard",
                    ScoreboardVisible: true,
                    OutputMode:
                        OverlayOutputMode.Programme,
                    ActiveGraphic: null,
                    GraphicVisible: false));

    [Fact]
    public async Task PublishAsync_PublishesCompleteCanonicalStateToMatchGroup()
    {
        var stateService =
            new Mock<IBroadcastPresentationStateService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var hubClients =
            new Mock<IHubClients>();

        var clientProxy =
            new Mock<IClientProxy>();

        var expected =
            State();

        stateService
            .Setup(s => s.CreateStateAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        hubContext
            .SetupGet(h => h.Clients)
            .Returns(hubClients.Object);

        hubClients
            .Setup(c => c.Group("match:m1"))
            .Returns(clientProxy.Object);

        clientProxy
            .Setup(c => c.SendCoreAsync(
                SignalRBroadcastPresentationPublisher.ClientMethod,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new SignalRBroadcastPresentationPublisher(
                stateService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRBroadcastPresentationPublisher>.Instance);

        var result =
            await sut.PublishAsync(
                "m1",
                default);

        result.Should()
            .BeSameAs(expected);

        stateService.Verify(
            s => s.CreateStateAsync(
                "m1",
                It.IsAny<CancellationToken>()),
            Times.Once);

        hubClients.Verify(
            c => c.Group("match:m1"),
            Times.Once);

        clientProxy.Verify(
            c => c.SendCoreAsync(
                SignalRBroadcastPresentationPublisher.ClientMethod,
                It.Is<object?[]>(
                    args =>
                        args.Length == 1 &&
                        ReferenceEquals(
                            args[0],
                            expected)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_UsesCanonicalBroadcastClientMethod()
    {
        var stateService =
            new Mock<IBroadcastPresentationStateService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var hubClients =
            new Mock<IHubClients>();

        var clientProxy =
            new Mock<IClientProxy>();

        stateService
            .Setup(s => s.CreateStateAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(State());

        hubContext
            .SetupGet(h => h.Clients)
            .Returns(hubClients.Object);

        hubClients
            .Setup(c => c.Group(
                It.IsAny<string>()))
            .Returns(clientProxy.Object);

        clientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new SignalRBroadcastPresentationPublisher(
                stateService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRBroadcastPresentationPublisher>.Instance);

        await sut.PublishAsync(
            "m1",
            default);

        clientProxy.Verify(
            c => c.SendCoreAsync(
                "BroadcastPresentationUpdated",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task PublishAsync_BlankMatchId_IsRejected(
        string matchId)
    {
        var stateService =
            new Mock<IBroadcastPresentationStateService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var sut =
            new SignalRBroadcastPresentationPublisher(
                stateService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRBroadcastPresentationPublisher>.Instance);

        var act =
            async () =>
                await sut.PublishAsync(
                    matchId,
                    default);

        await act.Should()
            .ThrowAsync<ArgumentException>();

        stateService.Verify(
            s => s.CreateStateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        hubContext.VerifyGet(
            h => h.Clients,
            Times.Never);
    }
}
