using AmharcAgent.Api.Publication;
using AmharcAgent.Core.Domain;
using DomainMatch = AmharcAgent.Core.Domain.Match;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class ClockSnapshotPublicationHostedServiceTests
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

    private static DomainMatch ActiveMatch() =>
        new()
        {
            MatchId = "m1",
            Status = MatchStatus.Active,
            CurrentPeriod = 1
        };

    [Fact]
    public async Task StateChanged_PublishesSnapshotForActiveMatch()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMatch());

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
                (AmharcAgent.Core.Contracts.ClockSnapshotV1)null!);

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<
                IServiceScopeFactory>();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                scopeFactory,
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await published.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

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
    public async Task StateChanged_WithNoActiveMatch_DoesNotPublish()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

        var repositoryObserved =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .Callback(
                () => repositoryObserved.TrySetResult(true))
            .ReturnsAsync((DomainMatch?)null);

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<
                IServiceScopeFactory>();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                scopeFactory,
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

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
    public async Task StopAsync_UnsubscribesFromClockStateChanged()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        await sut.StopAsync(
            default);

        clock.Raise(
            c => c.StateChanged += null!,
            ClockState());

        matches.Verify(
            m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        publisher.Verify(
            p => p.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RapidStateChanges_DoNotCauseOverlappingPublications()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMatch());

        var firstPublicationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseFirstPublication =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondPublicationCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var concurrent = 0;
        var maximumConcurrent = 0;
        var invocation = 0;

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
                            break;

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

                        return
                            (AmharcAgent.Core.Contracts.ClockSnapshotV1)
                                null!;
                    }
                    finally
                    {
                        Interlocked.Decrement(
                            ref concurrent);
                    }
                });

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await firstPublicationStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            for (var i = 0; i < 10; i++)
            {
                clock.Raise(
                    c => c.StateChanged += null!,
                    ClockState());
            }

            await Task.Delay(100);

            Volatile.Read(
                    ref maximumConcurrent)
                .Should().Be(1);

            releaseFirstPublication.TrySetResult(
                true);

            await secondPublicationCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            Volatile.Read(
                    ref maximumConcurrent)
                .Should().Be(1);
        }
        finally
        {
            releaseFirstPublication.TrySetResult(
                true);

            await sut.StopAsync(
                default);
        }
    }

    [Fact]
    public async Task PublicationFailure_DoesNotStopLaterPublication()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

        matches
            .Setup(m => m.GetActiveMatchAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMatch());

        var secondPublicationCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation = 0;

        publisher
            .Setup(p => p.PublishAsync(
                "m1",
                It.IsAny<CancellationToken>()))
            .Returns(
                () =>
                {
                    var current =
                        Interlocked.Increment(
                            ref invocation);

                    if (current == 1)
                    {
                        throw new InvalidOperationException(
                            "Simulated publication failure.");
                    }

                    secondPublicationCompleted
                        .TrySetResult(true);

                    return Task.FromResult(
                        (AmharcAgent.Core.Contracts.ClockSnapshotV1)
                            null!);
                });

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

        await sut.StartAsync(
            default);

        try
        {
            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await Task.Delay(100);

            clock.Raise(
                c => c.StateChanged += null!,
                ClockState());

            await secondPublicationCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(2));

            publisher.Verify(
                p => p.PublishAsync(
                    "m1",
                    It.IsAny<CancellationToken>()),
                Times.AtLeast(2));
        }
        finally
        {
            await sut.StopAsync(
                default);
        }
    }
    [Fact]
    public async Task ExplicitPublicationRequest_UsesProvidedMatchIdWithoutRepositoryLookup()
    {
        var clock =
            new Mock<IMatchClockService>();

        var matches =
            new Mock<IMatchRepository>();

        var publisher =
            new Mock<IClockSnapshotPublisher>();

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
                (AmharcAgent.Core.Contracts.ClockSnapshotV1)null!);

        var services =
            new ServiceCollection();

        services.AddScoped<IMatchRepository>(
            _ => matches.Object);

        await using var provider =
            services.BuildServiceProvider();

        var sut =
            new ClockSnapshotPublicationHostedService(
                clock.Object,
                provider.GetRequiredService<
                    IServiceScopeFactory>(),
                publisher.Object,
                NullLogger<
                    ClockSnapshotPublicationHostedService>.Instance);

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
    }}
