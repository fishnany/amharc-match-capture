using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.StreamDeck;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class StreamDeckCommandBridgeTests
{
    [Fact]
    public async Task ButtonPress_WithCommandId_DispatchesStreamDeckCommand()
    {
        var streamDeck = new Mock<IStreamDeckService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();

        AmharcCommand? capturedCommand = null;

        var dispatched = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<AmharcCommand, CancellationToken>(
                (command, _) =>
                {
                    capturedCommand = command;
                    dispatched.TrySetResult(true);
                })
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();

        services.AddScoped<IAmharcCommandDispatcher>(
            _ => dispatcher.Object);

        using var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<IServiceScopeFactory>();

        var bridge = new StreamDeckCommandBridge(
            streamDeck.Object,
            scopeFactory,
            NullLogger<StreamDeckCommandBridge>.Instance);

        bridge.Start();

        var button = new StreamDeckButton
        {
            ButtonNumber = 7,
            CommandId = AmharcCommandIds.ScoreHomeTwoPoint,
            Label = "HOME 2PT",
            Enabled = true
        };

        streamDeck.Raise(
            s => s.ButtonPressed += null,
            7,
            button);

        var completed = await Task.WhenAny(
            dispatched.Task,
            Task.Delay(1000));

        completed.Should().Be(
            dispatched.Task,
            "the Stream Deck command should be dispatched promptly");

        capturedCommand.Should().NotBeNull();

        capturedCommand!.CommandId
            .Should().Be(
                AmharcCommandIds.ScoreHomeTwoPoint);

        capturedCommand.Source
            .Should().Be(
                EventSource.StreamDeck);
    }

    [Fact]
    public async Task ButtonPress_WithoutCommandId_IsIgnored()
    {
        var streamDeck = new Mock<IStreamDeckService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();

        var services = new ServiceCollection();

        services.AddScoped<IAmharcCommandDispatcher>(
            _ => dispatcher.Object);

        using var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<IServiceScopeFactory>();

        var bridge = new StreamDeckCommandBridge(
            streamDeck.Object,
            scopeFactory,
            NullLogger<StreamDeckCommandBridge>.Instance);

        bridge.Start();

        var button = new StreamDeckButton
        {
            ButtonNumber = 2,
            Label = "LEGACY BUTTON",
            CommandId = null,
            Enabled = true
        };

        streamDeck.Raise(
            s => s.ButtonPressed += null,
            2,
            button);

        await Task.Delay(100);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Start_IsIdempotent_AndDoesNotDoubleDispatch()
    {
        var streamDeck = new Mock<IStreamDeckService>();
        var dispatcher = new Mock<IAmharcCommandDispatcher>();

        var dispatched = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Setup(d => d.DispatchAsync(
                It.IsAny<AmharcCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
                dispatched.TrySetResult(true))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();

        services.AddScoped<IAmharcCommandDispatcher>(
            _ => dispatcher.Object);

        using var provider =
            services.BuildServiceProvider();

        var bridge = new StreamDeckCommandBridge(
            streamDeck.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<StreamDeckCommandBridge>.Instance);

        bridge.Start();
        bridge.Start();

        var button = new StreamDeckButton
        {
            ButtonNumber = 1,
            CommandId = AmharcCommandIds.ScoreHomeGoal,
            Label = "HOME GOAL",
            Enabled = true
        };

        streamDeck.Raise(
            s => s.ButtonPressed += null,
            1,
            button);

        await Task.WhenAny(
            dispatched.Task,
            Task.Delay(1000));

        await Task.Delay(50);

        dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<AmharcCommand>(
                    c => c.CommandId ==
                         AmharcCommandIds.ScoreHomeGoal),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
