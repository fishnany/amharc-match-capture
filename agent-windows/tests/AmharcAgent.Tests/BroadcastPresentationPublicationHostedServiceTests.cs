using AmharcAgent.Api.Publication;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainMatch = AmharcAgent.Core.Domain.Match;

namespace AmharcAgent.Tests;

public class BroadcastPresentationPublicationHostedServiceTests
{
    private static ClockState ClockState() =>
        new(
            MatchClockSeconds: 120,
            RecordingElapsedSeconds: 150,
            IsRunning: true,
            CurrentPeriod: 1,
            PeriodStartTotalMatchElapsedSeconds: 0,
            ClockMode: "count-up",
            UpdatedAt: DateTimeOffset.UtcNow);

    private static OverlayState OverlayState() =>
        new(
            ActiveTemplateId: "scoreboard",
            IsVisible: true,
            OutputMode: OverlayOutputMode.Programme,
            CurrentGraphic: null,
            GraphicVisible: false,
            HomeGoals: 0,
            HomePoints: 0,
            AwayGoals: 0,
            AwayPoints: 0,
            MatchClockSeconds: 120,
            CurrentPeriod: 1);

    private static DomainMatch ActiveMatch() =>
        new()
        {
            MatchId = "m1",
            Status = MatchStatus.Active,
            CurrentPeriod = 1
        };

    private static BroadcastPresentationStateV1 State(
        string matchId) =>
        new(
            BroadcastPresentationStateV1.CurrentContractVersion,
            matchId,
            new BroadcastMatchIdentityV1(
                Competition: "Test Competition",
                Season: "2026",
                Round: "Round 1",
                HomeTeam: "HOME",
                AwayTeam: "AWAY",
                Venue: "AMHARC Test",
                Date: new DateOnly(2026, 9, 8)),
            new ScoreState(
                MatchId: matchId,
                Sport: Sport.GaelicFootball,
                ScoringModel:
                    ScoringModel.GoalsTwoPointOnePoint,
                HomeGoals: 0,
                HomeTwoPointScores: 0,
                HomePoints: 0,
                AwayGoals: 0,
                AwayTwoPointScores: 0,
                AwayPoints: 0,
                UpdatedAt: DateTimeOffset.UtcNow),
            new ClockSnapshotV1(
                ContractVersion: "1.0",
                MatchId: matchId,
                Period: 1,
                PeriodClockSeconds: 120,
                TotalMatchElapsedSeconds: 120,
                IsRunning: true,
                RecordingElapsedSeconds: 150,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                Authority:
                    new ClockAuthorityV1(
                        "amharc-match-capture",
                        "capture-instance-1"),
                AuthorityEpoch: 1,
                Sequence: 1),
            new BroadcastPresentationControlV1(
                ActiveTemplateId: "scoreboard",
                ScoreboardVisible: true,
                OutputMode: OverlayOutputMode.Programme,
                ActiveGraphic: null,
                GraphicVisible: false));

    private static ServiceProvider CreateProvider(
        Mock<IMatchRepository> matches,
        Mock<IBroadcastPresentationPublisher> publisher)
    {
        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        services.AddScoped<IBroadcastPresentationPublisher>(
            _ => publisher.Object);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExplicitRequest_UsesProvidedMatchIdWithoutRepositoryLookup()
    {
        var clock =
            new Mock<IMatchClockService>();

        var overlay =
            new Mock<IOverlayService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IBroadcastPresentationPublisher>();

        var published =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        publisher
            .Setup(p => p.PublishAsync(
                "explicit-match",
                It.IsAny<CancellationToken>()))
            .Callback(
                () => published.TrySetResult(true))
            .ReturnsAsync(
                State("explicit-match"));

        await using var provider =
            CreateProvider(
                matches,
                publisher);

        var sut =
            new BroadcastPresentationPublicationHostedService(
                clock.Object,
                overlay.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                NullLogger<
                    BroadcastPresentationPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            sut.RequestPublication(
                "explicit-match");

            await published.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            publisher.Verify(
                p => p.PublishAsync(
                    "explicit-match",
                    It.IsAny<CancellationToken>()),
                Times.Once);

            matches.Verify(
                m => m.GetActiveMatchAsync(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(
                default);
        }
    }

    [Fact]
    public async Task ClockStateChanged_PublishesForActiveMatch()
    {
        var clock =
            new Mock<IMatchClockService>();

        var overlay =
            new Mock<IOverlayService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IBroadcastPresentationPublisher>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ActiveMatch());

        var published =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        publisher
            .Setup(p => p.PublishAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Callback(
                () => published.TrySetResult(true))
            .ReturnsAsync(
                State("m1"));

        await using var provider =
            CreateProvider(
                matches,
                publisher);

        var sut =
            new BroadcastPresentationPublicationHostedService(
                clock.Object,
                overlay.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                NullLogger<
                    BroadcastPresentationPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await published.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            matches.Verify(
                m => m.GetActiveMatchAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

            publisher.Verify(
                p => p.PublishAsync(
                    "m1",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            await sut.StopAsync(
                default);
        }
    }

    [Fact]
    public async Task OverlayStateChanged_PublishesForActiveMatch()
    {
        var clock =
            new Mock<IMatchClockService>();

        var overlay =
            new Mock<IOverlayService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IBroadcastPresentationPublisher>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ActiveMatch());

        var published =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        publisher
            .Setup(p => p.PublishAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Callback(
                () => published.TrySetResult(true))
            .ReturnsAsync(
                State("m1"));

        await using var provider =
            CreateProvider(
                matches,
                publisher);

        var sut =
            new BroadcastPresentationPublicationHostedService(
                clock.Object,
                overlay.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                NullLogger<
                    BroadcastPresentationPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            overlay.Raise(
                o => o.StateChanged += null!,
                OverlayState());

            await published.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            matches.Verify(
                m => m.GetActiveMatchAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

            publisher.Verify(
                p => p.PublishAsync(
                    "m1",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            await sut.StopAsync(
                default);
        }
    }

    [Fact]
    public async Task ActiveMatchTrigger_WithNoActiveMatch_DoesNotPublish()
    {
        var clock =
            new Mock<IMatchClockService>();

        var overlay =
            new Mock<IOverlayService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IBroadcastPresentationPublisher>();

        var repositoryObserved =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .Callback(
                () => repositoryObserved.TrySetResult(true))
            .ReturnsAsync(
                (DomainMatch?)null);

        await using var provider =
            CreateProvider(
                matches,
                publisher);

        var sut =
            new BroadcastPresentationPublicationHostedService(
                clock.Object,
                overlay.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                NullLogger<
                    BroadcastPresentationPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await repositoryObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            publisher.Verify(
                p => p.PublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await sut.StopAsync(
                default);
        }
    }

    [Fact]
    public async Task RepeatedSameMatchRequests_AreCoalescedAndNeverOverlap()
    {
        var clock =
            new Mock<IMatchClockService>();

        var overlay =
            new Mock<IOverlayService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IBroadcastPresentationPublisher>();

        var firstPublicationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseFirstPublication =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondPublicationCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var concurrent =
            0;

        var maximumConcurrent =
            0;

        var invocation =
            0;

        publisher
            .Setup(p => p.PublishAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Returns(
                async () =>
                {
                    var currentConcurrent =
                        Interlocked.Increment(
                            ref concurrent);

                    var observedMaximum =
                        Volatile.Read(
                            ref maximumConcurrent);

                    while (
                        currentConcurrent >
                        observedMaximum)
                    {
                        var previous =
                            Interlocked.CompareExchange(
                                ref maximumConcurrent,
                                currentConcurrent,
                                observedMaximum);

                        if (previous == observedMaximum)
                        {
                            break;
                        }

                        observedMaximum =
                            previous;
                    }

                    try
                    {
                        var currentInvocation =
                            Interlocked.Increment(
                                ref invocation);

                        if (currentInvocation == 1)
                        {
                            firstPublicationStarted
                                .TrySetResult(true);

                            await releaseFirstPublication
                                .Task;
                        }
                        else
                        {
                            secondPublicationCompleted
                                .TrySetResult(true);
                        }

                        return State("m1");
                    }
                    finally
                    {
                        Interlocked.Decrement(
                            ref concurrent);
                    }
                });

        await using var provider =
            CreateProvider(
                matches,
                publisher);

        var sut =
            new BroadcastPresentationPublicationHostedService(
                clock.Object,
                overlay.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                NullLogger<
                    BroadcastPresentationPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            sut.RequestPublication(
                "m1");

            await firstPublicationStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            for (var i = 0; i < 10; i++)
            {
                sut.RequestPublication(
                    "m1");
            }

            await Task.Delay(100);

            Volatile.Read(
                    ref maximumConcurrent)
                .Should()
                .Be(1);

            releaseFirstPublication.TrySetResult(
                true);

            await secondPublicationCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            Volatile.Read(
                    ref maximumConcurrent)
                .Should()
                .Be(1);

            Volatile.Read(
                    ref invocation)
                .Should()
                .Be(2);
        }
        finally
        {
            releaseFirstPublication.TrySetResult(
                true);

            await sut.StopAsync(
                default);
        }
    }
}
