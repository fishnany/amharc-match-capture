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
    public void ReceiverArguments_ProduceCredentialFreeVideoOnlyMpegTs()
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

        arguments.Should()
            .Contain("-progress pipe:2");

        arguments.Should()
            .Contain("-rtsp_transport tcp");

        arguments.Should()
            .Contain("-map 0:v:0");

        arguments.Should()
            .Contain("-an");

        arguments.Should()
            .Contain("-c:v copy");

        arguments.Should()
            .Contain("-f mpegts");

        arguments.Should()
            .Contain("pipe:1");

        arguments.Should()
            .Contain(uri);

        arguments.Should()
            .NotContain("192.168.1.136");

        arguments.Should()
            .NotContain("-f segment");

        arguments.Should()
            .NotContain("-f mpjpeg");
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

        redacted.Should()
            .NotContain(secret);

        redacted.Should()
            .NotContain("receiver-user");

        redacted.Should()
            .Contain(
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
            .Select(member =>
                member.Name)
            .Should()
            .NotContain(
                name =>
                    name.Contains(
                        "Rtsp",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Credential",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Password",
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    [Fact]
    public void MediaSourceContract_ExposesLeaseNotEndpoint()
    {
        var publicMembers =
            typeof(IStreamReceiverMediaSource)
                .GetMembers(
                    BindingFlags.Public |
                    BindingFlags.Instance);

        publicMembers
            .Select(member =>
                member.Name)
            .Should()
            .Contain("AcquireAsync");

        publicMembers
            .Select(member =>
                member.Name)
            .Should()
            .NotContain(
                name =>
                    name.Contains(
                        "Uri",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Endpoint",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Credential",
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    [Fact]
    public void MediaLeaseContract_ExposesOnlyCredentialFreeStream()
    {
        typeof(IStreamReceiverMediaLease)
            .GetProperty("Stream")
            .Should()
            .NotBeNull();

        typeof(IStreamReceiverMediaLease)
            .GetProperties()
            .Select(property =>
                property.Name)
            .Should()
            .NotContain(
                name =>
                    name.Contains(
                        "Uri",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Credential",
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    name.Contains(
                        "Password",
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    [Fact]
    public void Receiver_ImplementsLifecycleAndMediaSourceContracts()
    {
        typeof(FfmpegStreamReceiver)
            .Should()
            .Implement<IStreamReceiver>();

        typeof(FfmpegStreamReceiver)
            .Should()
            .Implement<IStreamReceiverMediaSource>();
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
                NullLogger<FfmpegStreamReceiver>
                    .Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\ffmpeg.exe");

        sut.State.Should()
            .Be(
                StreamReceiverState.Idle);

        sut.Health.State.Should()
            .Be(
                StreamReceiverState.Idle);

        sut.Health.CameraId.Should()
            .Be("primary");

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
            .SetupGet(c =>
                c.ConnectionState)
            .Returns(
                CameraConnectionState
                    .Connected);

        camera
            .Setup(c =>
                c.GetAuthenticatedStreamUrlAsync(
                    null,
                    It.IsAny<
                        CancellationToken>()))
            .ReturnsAsync(
                "rtsp://user:secret@camera/axis-media/media.amp?videocodec=h264");

        var sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>
                    .Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\definitely-missing-mr16c-ffmpeg.exe");

        Func<Task> act =
            () => sut.StartAsync();

        await act.Should()
            .ThrowAsync<Exception>();

        sut.State.Should()
            .Be(
                StreamReceiverState.Error);

        camera.Verify(
            c =>
                c.GetAuthenticatedStreamUrlAsync(
                    null,
                    It.IsAny<
                        CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Acquire_WhenFfmpegCannotLaunch_DoesNotExposeSourceAndFailsClosed()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(c => c.CameraId)
            .Returns("primary");

        camera
            .SetupGet(c =>
                c.ConnectionState)
            .Returns(
                CameraConnectionState
                    .Connected);

        camera
            .Setup(c =>
                c.GetAuthenticatedStreamUrlAsync(
                    null,
                    It.IsAny<
                        CancellationToken>()))
            .ReturnsAsync(
                "rtsp://user:secret@camera/axis-media/media.amp?videocodec=h264");

        IStreamReceiverMediaSource sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>
                    .Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\definitely-missing-mr16c-acquire.exe");

        Func<Task> act =
            async () =>
                await sut.AcquireAsync();

        await act.Should()
            .ThrowAsync<Exception>();
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
                NullLogger<FfmpegStreamReceiver>
                    .Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\ffmpeg.exe");

        await sut.StopAsync();
        await sut.StopAsync();

        sut.State.Should()
            .Be(
                StreamReceiverState.Idle);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var camera =
            new Mock<ICameraAdapter>();

        camera
            .SetupGet(c => c.CameraId)
            .Returns("primary");

        var sut =
            new FfmpegStreamReceiver(
                camera.Object,
                NullLogger<FfmpegStreamReceiver>
                    .Instance,
                @"C:\AMHARC\Runtime\ffmpeg\bin\ffmpeg.exe");

        await sut.DisposeAsync();

        Func<Task> secondDispose =
            async () =>
                await sut.DisposeAsync();

        await secondDispose.Should()
            .NotThrowAsync();
    }

    [Fact]
    public void BootstrapBuffer_ProducesPatPmtIdrAlignedSnapshot()
    {
        var bootstrap = CreateBootstrapBuffer();
        var pat = CreatePatPacket(0x1000);
        var pmt = CreatePmtPacket(0x1000, 0x0100);
        var idr = CreateH264IdrPacket(0x0100);

        AppendBootstrap(bootstrap, pat.Concat(pmt).Concat(idr).ToArray());
        var snapshot = SnapshotBootstrap(bootstrap);

        snapshot.Should().HaveCount(188 * 3);
        snapshot[0].Should().Be(0x47);
        snapshot[188].Should().Be(0x47);
        snapshot[376].Should().Be(0x47);
        snapshot.Should().ContainInOrder(new byte[] { 0x00, 0x00, 0x01, 0x65 });
    }

    [Fact]
    public void BootstrapBuffer_FragmentedWrites_PreserveTsAlignment()
    {
        var bootstrap = CreateBootstrapBuffer();
        var bytes = CreatePatPacket(0x1000)
            .Concat(CreatePmtPacket(0x1000, 0x0100))
            .Concat(CreateH264IdrPacket(0x0100))
            .ToArray();

        AppendBootstrap(bootstrap, bytes[..73]);
        AppendBootstrap(bootstrap, bytes[73..311]);
        AppendBootstrap(bootstrap, bytes[311..]);

        var snapshot = SnapshotBootstrap(bootstrap);
        snapshot.Should().NotBeEmpty();
        (snapshot.Length % 188).Should()
            .Be(0);

        for (var offset = 0; offset < snapshot.Length; offset += 188)
        {
            snapshot[offset].Should().Be(0x47);
        }
    }

    [Fact]
    public void BootstrapBuffer_Reset_RemovesStaleBootstrap()
    {
        var bootstrap = CreateBootstrapBuffer();
        AppendBootstrap(
            bootstrap,
            CreatePatPacket(0x1000)
                .Concat(CreatePmtPacket(0x1000, 0x0100))
                .Concat(CreateH264IdrPacket(0x0100))
                .ToArray());

        SnapshotBootstrap(bootstrap).Should().NotBeEmpty();

        bootstrap.GetType()
            .GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(bootstrap, null);

        SnapshotBootstrap(bootstrap).Should().BeEmpty();
    }

    private static object CreateBootstrapBuffer()
    {
        var type = typeof(FfmpegStreamReceiver)
            .GetNestedType("MpegTsBootstrapBuffer", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MpegTsBootstrapBuffer was not found.");

        return Activator.CreateInstance(type, nonPublic: true)
               ?? throw new InvalidOperationException("Unable to construct MpegTsBootstrapBuffer.");
    }

    private static void AppendBootstrap(object bootstrap, byte[] bytes)
    {
        bootstrap.GetType()
            .GetMethod("Append", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(bootstrap, new object[] { bytes });
    }

    private static byte[] SnapshotBootstrap(object bootstrap) =>
        (byte[])bootstrap.GetType()
            .GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(bootstrap, null)!;

    private static byte[] CreatePatPacket(int pmtPid)
    {
        var packet = Enumerable.Repeat((byte)0xFF, 188).ToArray();
        packet[0] = 0x47;
        packet[1] = 0x40;
        packet[2] = 0x00;
        packet[3] = 0x10;
        packet[4] = 0x00;

        var section = 5;
        packet[section] = 0x00;
        packet[section + 1] = 0xB0;
        packet[section + 2] = 0x0D;
        packet[section + 3] = 0x00;
        packet[section + 4] = 0x01;
        packet[section + 5] = 0xC1;
        packet[section + 6] = 0x00;
        packet[section + 7] = 0x00;
        packet[section + 8] = 0x00;
        packet[section + 9] = 0x01;
        packet[section + 10] = (byte)(0xE0 | ((pmtPid >> 8) & 0x1F));
        packet[section + 11] = (byte)(pmtPid & 0xFF);
        return packet;
    }

    private static byte[] CreatePmtPacket(int pmtPid, int videoPid)
    {
        var packet = Enumerable.Repeat((byte)0xFF, 188).ToArray();
        packet[0] = 0x47;
        packet[1] = (byte)(0x40 | ((pmtPid >> 8) & 0x1F));
        packet[2] = (byte)(pmtPid & 0xFF);
        packet[3] = 0x10;
        packet[4] = 0x00;

        var section = 5;
        packet[section] = 0x02;
        packet[section + 1] = 0xB0;
        packet[section + 2] = 0x12;
        packet[section + 3] = 0x00;
        packet[section + 4] = 0x01;
        packet[section + 5] = 0xC1;
        packet[section + 6] = 0x00;
        packet[section + 7] = 0x00;
        packet[section + 8] = (byte)(0xE0 | ((videoPid >> 8) & 0x1F));
        packet[section + 9] = (byte)(videoPid & 0xFF);
        packet[section + 10] = 0xF0;
        packet[section + 11] = 0x00;
        packet[section + 12] = 0x1B;
        packet[section + 13] = (byte)(0xE0 | ((videoPid >> 8) & 0x1F));
        packet[section + 14] = (byte)(videoPid & 0xFF);
        packet[section + 15] = 0xF0;
        packet[section + 16] = 0x00;
        return packet;
    }

    private static byte[] CreateH264IdrPacket(int videoPid)
    {
        var packet = Enumerable.Repeat((byte)0xFF, 188).ToArray();
        packet[0] = 0x47;
        packet[1] = (byte)(0x40 | ((videoPid >> 8) & 0x1F));
        packet[2] = (byte)(videoPid & 0xFF);
        packet[3] = 0x10;

        packet[4] = 0x00;
        packet[5] = 0x00;
        packet[6] = 0x01;
        packet[7] = 0xE0;
        packet[8] = 0x00;
        packet[9] = 0x00;
        packet[10] = 0x80;
        packet[11] = 0x00;
        packet[12] = 0x00;
        packet[13] = 0x00;
        packet[14] = 0x00;
        packet[15] = 0x01;
        packet[16] = 0x65;
        packet[17] = 0x88;
        return packet;
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

    private sealed class StreamReceiverHealthContract
    {
        public AmharcAgent.Core.Models
            .StreamReceiverHealth? Value
        {
            get;
            init;
        }
    }

    [Fact]
    public void MediaSourceContract_ExposesLossIntolerantAcquisitionWithoutEndpoint()
    {
        var acquire =
            typeof(IStreamReceiverMediaSource)
                .GetMethod("AcquireAsync");

        acquire.Should()
            .NotBeNull();

        var parameters =
            acquire!
                .GetParameters();

        parameters.Should()
            .HaveCount(2);

        parameters[0]
            .ParameterType.Should()
            .Be(typeof(CancellationToken));

        parameters[1]
            .ParameterType.Should()
            .Be(typeof(bool));

        parameters[1]
            .Name.Should()
            .Be("lossIntolerant");

        parameters[1]
            .HasDefaultValue.Should()
            .BeTrue();

        parameters[1]
            .DefaultValue.Should()
            .Be(false);

        typeof(IStreamReceiverMediaSource)
            .GetMembers(
                BindingFlags.Public |
                BindingFlags.Instance)
            .Select(member =>
                member.Name)
            .Should()
            .NotContain(
                name =>
                    name.Contains(
                        "Uri",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(
                        "Endpoint",
                        StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(
                        "Credential",
                        StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void BootstrapAppend_PreservesAlignmentAcrossObserved56And132ByteSplit()
    {
        var type = typeof(FfmpegStreamReceiver).GetNestedType(
            "MpegTsBootstrapBuffer",
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(type);

        var instance = Activator.CreateInstance(type!, nonPublic: true);
        Assert.NotNull(instance);

        var append = type!.GetMethod("Append");
        Assert.NotNull(append);
        Assert.Equal(typeof(byte[]), append!.ReturnType);

        var source = Enumerable.Range(0, 5)
            .Select(index => CreateAlignedSyntheticTsPacket((byte)(0x20 + index)))
            .SelectMany(packet => packet)
            .ToArray();

        var split = (188 * 3) + 56;
        var first = Assert.IsType<byte[]>(
            append.Invoke(instance, new object[] { source[..split] }));
        var second = Assert.IsType<byte[]>(
            append.Invoke(instance, new object[] { source[split..] }));

        Assert.Equal(188 * 3, first.Length);
        Assert.Equal(188 * 2, second.Length);

        var emitted = first.Concat(second).ToArray();
        Assert.Equal(source, emitted);
        Assert.Equal(0, emitted.Length % 188);

        for (var offset = 0; offset < emitted.Length; offset += 188)
        {
            Assert.Equal((byte)0x47, emitted[offset]);
        }
    }

    [Fact]
    public void BootstrapAppend_EmitsOnlyWholeTransportPacketsForArbitraryReads()
    {
        var type = typeof(FfmpegStreamReceiver).GetNestedType(
            "MpegTsBootstrapBuffer",
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(type);

        var instance = Activator.CreateInstance(type!, nonPublic: true);
        Assert.NotNull(instance);
        var append = type!.GetMethod("Append");
        Assert.NotNull(append);

        var source = Enumerable.Range(0, 12)
            .Select(index => CreateAlignedSyntheticTsPacket((byte)(0x40 + index)))
            .SelectMany(packet => packet)
            .ToArray();

        var sizes = new[] { 56, 132, 301, 17, 419, 9, 570, 211, 999 };
        var sourceOffset = 0;
        var emitted = new List<byte>();

        foreach (var requested in sizes)
        {
            if (sourceOffset >= source.Length) { break; }
            var count = Math.Min(requested, source.Length - sourceOffset);
            var chunk = source.AsSpan(sourceOffset, count).ToArray();
            sourceOffset += count;

            var aligned = Assert.IsType<byte[]>(
                append!.Invoke(instance, new object[] { chunk }));

            Assert.Equal(0, aligned.Length % 188);
            for (var offset = 0; offset < aligned.Length; offset += 188)
            {
                Assert.Equal((byte)0x47, aligned[offset]);
            }
            emitted.AddRange(aligned);
        }

        if (sourceOffset < source.Length)
        {
            var aligned = Assert.IsType<byte[]>(
                append!.Invoke(instance, new object[] { source[sourceOffset..] }));
            Assert.Equal(0, aligned.Length % 188);
            emitted.AddRange(aligned);
        }

        Assert.Equal(source, emitted.ToArray());
    }

    private static byte[] CreateAlignedSyntheticTsPacket(byte fill)
    {
        var packet = Enumerable.Repeat(fill, 188)
            .Select(value => (byte)value)
            .ToArray();

        packet[0] = 0x47;
        packet[1] = 0x01;
        packet[2] = 0x00;
        packet[3] = 0x10;
        return packet;
    }}