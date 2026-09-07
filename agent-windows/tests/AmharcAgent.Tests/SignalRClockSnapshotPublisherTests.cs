using AmharcAgent.Api.Hubs;
using AmharcAgent.Api.Publication;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class SignalRClockSnapshotPublisherTests
{
    private static ClockSnapshotV1 Snapshot() =>
        new(
            ContractVersion: "1.0",
            MatchId: "m1",
            Period: 2,
            PeriodClockSeconds: 245,
            TotalMatchElapsedSeconds: 2345,
            IsRunning: true,
            RecordingElapsedSeconds: 2500,
            ObservedAtUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    7,
                    15,
                    30,
                    0,
                    TimeSpan.Zero),
            Authority:
                new ClockAuthorityV1(
                    "AMHARC Capture",
                    "capture-instance-1"),
            AuthorityEpoch: 123456,
            Sequence: 42);

    [Fact]
    public async Task PublishAsync_PublishesCanonicalSnapshotToMatchGroup()
    {
        var snapshotService =
            new Mock<ICanonicalClockSnapshotService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var hubClients =
            new Mock<IHubClients>();

        var clientProxy =
            new Mock<IClientProxy>();

        var expected =
            Snapshot();

        snapshotService
            .Setup(s => s.CreateSnapshot("m1"))
            .Returns(expected);

        hubContext
            .SetupGet(h => h.Clients)
            .Returns(hubClients.Object);

        hubClients
            .Setup(c => c.Group("match:m1"))
            .Returns(clientProxy.Object);

        clientProxy
            .Setup(c => c.SendCoreAsync(
                SignalRClockSnapshotPublisher.ClientMethod,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new SignalRClockSnapshotPublisher(
                snapshotService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRClockSnapshotPublisher>.Instance);

        var result =
            await sut.PublishAsync(
                "m1",
                default);

        result.Should().BeSameAs(expected);

        snapshotService.Verify(
            s => s.CreateSnapshot("m1"),
            Times.Once);

        hubClients.Verify(
            c => c.Group("match:m1"),
            Times.Once);

        clientProxy.Verify(
            c => c.SendCoreAsync(
                SignalRClockSnapshotPublisher.ClientMethod,
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
    public async Task PublishAsync_UsesCanonicalClientMethodName()
    {
        var snapshotService =
            new Mock<ICanonicalClockSnapshotService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var hubClients =
            new Mock<IHubClients>();

        var clientProxy =
            new Mock<IClientProxy>();

        snapshotService
            .Setup(s => s.CreateSnapshot("m1"))
            .Returns(Snapshot());

        hubContext
            .SetupGet(h => h.Clients)
            .Returns(hubClients.Object);

        hubClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(clientProxy.Object);

        clientProxy
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new SignalRClockSnapshotPublisher(
                snapshotService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRClockSnapshotPublisher>.Instance);

        await sut.PublishAsync(
            "m1",
            default);

        clientProxy.Verify(
            c => c.SendCoreAsync(
                "ClockSnapshotUpdated",
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
        var snapshotService =
            new Mock<ICanonicalClockSnapshotService>();

        var hubContext =
            new Mock<IHubContext<MatchHub>>();

        var sut =
            new SignalRClockSnapshotPublisher(
                snapshotService.Object,
                hubContext.Object,
                NullLogger<
                    SignalRClockSnapshotPublisher>.Instance);

        var act =
            async () =>
                await sut.PublishAsync(
                    matchId,
                    default);

        await act.Should()
            .ThrowAsync<ArgumentException>();

        snapshotService.Verify(
            s => s.CreateSnapshot(
                It.IsAny<string>()),
            Times.Never);

        hubContext.VerifyGet(
            h => h.Clients,
            Times.Never);
    }
}
