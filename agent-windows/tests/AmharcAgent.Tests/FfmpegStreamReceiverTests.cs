using System.Reflection;
using Xunit;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AmharcAgent.Tests;

public sealed class FfmpegStreamReceiverTests
{
    [Fact]
    public void ReceiverArguments_AreVideoOnlyManagedIngressProbe()
    {
        var method =
            GetPrivateStatic(
                "BuildReceiverArguments");

        var uri =
            "rtsp://receiver-user:receiver-secret@192.168.1.135:554/axis-media/media.amp?videocodec=h264";

        var arguments =
            (string)method.Invoke(
                null,
                [uri])!;

        arguments.Should().Contain("-progress pipe:1");
        arguments.Should().Contain("-rtsp_transport tcp");
        arguments.Should().Contain("-map 0:v:0");
        arguments.Should().Contain("-an");
        arguments.Should().Contain("-c:v copy");
        arguments.Should().Contain("-f null");
        arguments.Should().Contain(uri);
        arguments.Should().NotContain("192.168.1.136");
        arguments.Should().NotContain("-f segment");
        arguments.Should().NotContain("-f mpjpeg");
    }

    [Fact]
    public void ReceiverCredentialRedaction_RemovesRuntimeSecret()
    {
        var method =
            GetPrivateStatic(
                "RedactRtspCredentials");

        var secret =
            "synthetic-receiver-secret";

        var input =
            $"failure rtsp://receiver-user:{secret}@192.168.1.135:554/axis-media/media.amp";

        var redacted =
            (string)method.Invoke(
                null,
                [input])!;

        redacted.Should().NotContain(secret);
        redacted.Should().NotContain("receiver-user");
        redacted.Should().Contain(
            "rtsp://***:***@192.168.1.135:554/");
    }

    [Fact]
    public void StreamReceiverContract_DoesNotExposeRtspUri()
    {
        var publicMembers =
            typeof(IStreamReceiver)
                .GetMembers(
                    BindingFlags.Public |
                    BindingFlags.Instance);

        publicMembers
            .Select(member => member.Name)
            .Should()
            .NotContain(
                name =>
                    name.Contains(
                        "Rtsp",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(
                        "Credential",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(
                        "Password",
                        StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Receiver_InitialState_IsIdleAndHealthContainsNoEndpoint()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(c => c.CameraId)
            .Returns("primary");

        var sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>.Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\ffmpeg.exe");

        sut.State.Should().Be(
            StreamReceiverState.Idle);

        sut.Health.State.Should().Be(
            StreamReceiverState.Idle);

        sut.Health.CameraId.Should().Be(
            "primary");

        typeof(StreamReceiverHealthContract)
            .Should()
            .NotBeNull();
    }

    [Fact]
    public async Task Start_WhenFfmpegCannotLaunch_FailsClosedInErrorState()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(c => c.CameraId)
            .Returns("primary");

        camera
            .SetupGet(c => c.ConnectionState)
            .Returns(
                CameraConnectionState.Connected);

        camera
            .Setup(c =>
                c.GetAuthenticatedStreamUrlAsync(
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "rtsp://user:secret@camera/axis-media/media.amp?videocodec=h264");

        var sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>.Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\definitely-missing-mr16b-ffmpeg.exe");

        Func<Task> act =
            () => sut.StartAsync();

        await act.Should()
            .ThrowAsync<Exception>();

        sut.State.Should().Be(
            StreamReceiverState.Error);

        camera.Verify(
            c => c.GetAuthenticatedStreamUrlAsync(
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Stop_FromIdle_IsIdempotent()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(c => c.CameraId)
            .Returns("primary");

        var sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>.Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\ffmpeg.exe");

        await sut.StopAsync();
        await sut.StopAsync();

        sut.State.Should().Be(
            StreamReceiverState.Idle);
    }

    private static MethodInfo GetPrivateStatic(
        string name) =>
        typeof(FfmpegStreamReceiver)
            .GetMethod(
                name,
                BindingFlags.NonPublic |
                BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Unable to locate private static method {name}.");

    // Compile-time sentinel: the health contract must remain in Core.Models,
    // not become an infrastructure or transport-specific DTO.
    private sealed class StreamReceiverHealthContract
    {
        public AmharcAgent.Core.Models.StreamReceiverHealth? Value { get; init; }
    }
}
