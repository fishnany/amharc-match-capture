using AmharcAgent.Api.Runtime;
using AmharcAgent.Core.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class StreamReceiverLifecycleHostedServiceTests
{
    [Fact]
    public async Task StartAsync_PreservesLazyCanonicalIngress()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        await sut.StartAsync(
            CancellationToken.None);

        receiver.Verify(
            r => r.StartAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        receiver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_DelegatesToCanonicalReceiver()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        receiver
            .Setup(r => r.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        await sut.StopAsync(
            CancellationToken.None);

        receiver.Verify(
            r => r.StopAsync(
                CancellationToken.None),
            Times.Once);

        receiver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_PropagatesHostCancellationToken()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        using var cts =
            new CancellationTokenSource();

        var token =
            cts.Token;

        receiver
            .Setup(r => r.StopAsync(token))
            .Returns(Task.CompletedTask);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        await sut.StopAsync(token);

        receiver.Verify(
            r => r.StopAsync(token),
            Times.Once);

        receiver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_AwaitsCanonicalReceiverShutdown()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        var shutdownCompleted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        receiver
            .Setup(r => r.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(shutdownCompleted.Task);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        var stopTask =
            sut.StopAsync(
                CancellationToken.None);

        stopTask.IsCompleted.Should()
            .BeFalse();

        shutdownCompleted.TrySetResult(true);

        await stopTask;

        receiver.Verify(
            r => r.StopAsync(
                CancellationToken.None),
            Times.Once);

        receiver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_PropagatesCanonicalReceiverShutdownFailure()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        var expected =
            new InvalidOperationException(
                "Synthetic canonical receiver shutdown failure.");

        receiver
            .Setup(r => r.StopAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        Func<Task> act =
            () => sut.StopAsync(
                CancellationToken.None);

        var assertion =
            await act.Should()
                .ThrowAsync<InvalidOperationException>();

        assertion.Which.Should()
            .BeSameAs(expected);

        receiver.Verify(
            r => r.StopAsync(
                CancellationToken.None),
            Times.Once);

        receiver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RepeatedStopAsync_DelegatesToIdempotentReceiverLifecycle()
    {
        var receiver =
            new Mock<IStreamReceiver>(
                MockBehavior.Strict);

        receiver
            .Setup(r => r.StopAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut =
            new StreamReceiverLifecycleHostedService(
                receiver.Object);

        await sut.StopAsync(
            CancellationToken.None);

        await sut.StopAsync(
            CancellationToken.None);

        receiver.Verify(
            r => r.StopAsync(
                CancellationToken.None),
            Times.Exactly(2));

        receiver.VerifyNoOtherCalls();
    }
}