using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Audio;
using FluentAssertions;
using Moq;
using Xunit;

namespace AmharcAgent.Tests;

public class AudioRuntimeHealthObserverTests
{
    [Fact]
    public async Task MissingCredential_PublishesBlockedWithoutProbing()
    {
        var credentials = new Mock<IAudioCredentialProvider>();
        credentials.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AudioCredentialResolution.Missing());
        var probe = new Mock<IAudioRuntimeProbe>();
        var publisher = new Mock<IAudioRuntimeHealthPublisher>();
        AudioRuntimeHealthState? published = null;
        publisher.Setup(x => x.Publish(It.IsAny<AudioRuntimeHealthState>()))
            .Callback<AudioRuntimeHealthState>(x => published = x);

        var observer = new AudioRuntimeHealthObserver(
            credentials.Object, probe.Object, publisher.Object);
        await observer.ObserveOnceAsync();

        probe.Verify(
            x => x.ProbeAsync(It.IsAny<AudioCredential>(), It.IsAny<CancellationToken>()),
            Times.Never);
        published.Should().NotBeNull();
        published!.Status.Should().Be(AudioRuntimeHealthStatus.Blocked);
    }

    [Fact]
    public async Task AuthenticationRejected_PublishesBlocked()
    {
        var credentials = AvailableCredentials();
        var probe = new Mock<IAudioRuntimeProbe>();
        probe.Setup(x => x.ProbeAsync(It.IsAny<AudioCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioRuntimeProbeResult(
                true, false, false, false, null, null, null, null,
                AudioRuntimeProbeFailure.AuthenticationRejected, "Authentication rejected."));
        var publisher = new Mock<IAudioRuntimeHealthPublisher>();
        AudioRuntimeHealthState? published = null;
        publisher.Setup(x => x.Publish(It.IsAny<AudioRuntimeHealthState>()))
            .Callback<AudioRuntimeHealthState>(x => published = x);

        await new AudioRuntimeHealthObserver(
            credentials.Object, probe.Object, publisher.Object).ObserveOnceAsync();

        published!.Status.Should().Be(AudioRuntimeHealthStatus.Blocked);
    }

    [Fact]
    public async Task ValidMediaFlow_PublishesReady()
    {
        var credentials = AvailableCredentials();
        var probe = new Mock<IAudioRuntimeProbe>();
        probe.Setup(x => x.ProbeAsync(It.IsAny<AudioCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioRuntimeProbeResult(
                true, true, true, true, "mpeg4-generic", 8000, 1, 97,
                AudioRuntimeProbeFailure.None, "Audio RTP flow observed."));
        var publisher = new Mock<IAudioRuntimeHealthPublisher>();
        AudioRuntimeHealthState? published = null;
        publisher.Setup(x => x.Publish(It.IsAny<AudioRuntimeHealthState>()))
            .Callback<AudioRuntimeHealthState>(x => published = x);

        await new AudioRuntimeHealthObserver(
            credentials.Object, probe.Object, publisher.Object).ObserveOnceAsync();

        published!.Status.Should().Be(AudioRuntimeHealthStatus.Ready);
        published.MediaFlowObserved.Should().BeTrue();
    }

    private static Mock<IAudioCredentialProvider> AvailableCredentials()
    {
        var mock = new Mock<IAudioCredentialProvider>();
        mock.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AudioCredentialResolution.Available(
                new AudioCredential("synthetic-user", "synthetic-test-secret")));
        return mock;
    }
}
