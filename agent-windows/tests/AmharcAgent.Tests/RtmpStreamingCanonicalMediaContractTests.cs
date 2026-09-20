using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.Streaming;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class RtmpStreamingCanonicalMediaContractTests
{
    private static readonly string StreamingSource =
        File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "agent-windows",
                "src",
                "AmharcAgent.Infrastructure",
                "Streaming",
                "RtmpStreamingService.cs"));

    [Fact]
    public void StreamingService_DependsOnCanonicalMediaSource()
    {
        var constructor =
            typeof(RtmpStreamingService)
                .GetConstructors()
                .Single();

        var parameterTypes =
            constructor
                .GetParameters()
                .Select(
                    parameter =>
                        parameter.ParameterType)
                .ToArray();

        Assert.Contains(
            typeof(IStreamReceiverMediaSource),
            parameterTypes);

        Assert.DoesNotContain(
            typeof(ICameraAdapter),
            parameterTypes);
    }
    [Fact]
    public void StreamingService_UsesCanonicalVideoAndSeparateAuthoritativeAudioIngress()
    {
        var source = StreamingSource;

        // R1-03 preserves the R1-02 canonical video authority.
        Assert.Contains(
            "IStreamReceiverMediaSource",
            source);

        Assert.Contains(
            "lossIntolerant: false",
            source);

        Assert.Contains(
            "-f mpegts",
            source);

        Assert.Contains(
            "-i pipe:0",
            source);

        Assert.Contains(
            "-map 0:v:0",
            source);

        // Authoritative audio is deliberately independent of
        // canonical video ingress.
        Assert.Contains(
            "IRecordingAudioSourceResolver",
            source);

        Assert.Contains(
            "-rtsp_transport tcp",
            source);

        Assert.Contains(
            "-map 1:a:0",
            source);

        // Streaming must not take ownership of the canonical
        // receiver or bypass the existing audio authority.
        Assert.DoesNotContain(
            "IStreamReceiver ",
            source);

        Assert.DoesNotContain(
            "IAudioCredentialProvider",
            source);
    }

    [Fact]
    public void StreamingService_ConsumesCanonicalMpegTsFromStandardInput()
    {
        Assert.Contains(
            "-f mpegts",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.Contains(
            "-i pipe:0",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.Contains(
            "StandardInput.BaseStream",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.Contains(
            "lease.Stream.CopyToAsync",
            StreamingSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StreamingService_UsesIndependentBoundedConsumerLease()
    {
        Assert.Contains(
            "lossIntolerant: false",
            StreamingSource,
            StringComparison.Ordinal);
    }
    [Fact]
    public void StreamingService_RequiresAuthoritativeAudioForNormalStreaming()
    {
        var source = StreamingSource;

        Assert.Contains(
            "IRecordingAudioSourceResolver",
            source);

        Assert.Contains(
            "Authoritative audio source is unavailable",
            source);

        Assert.Contains(
            "-map 0:v:0",
            source);

        Assert.Contains(
            "-map 1:a:0",
            source);

        Assert.Contains(
            "-c:a aac",
            source);

        Assert.Contains(
            "-b:a 128k",
            source);

        // Completed R1-03 normal Streaming is AV.
        // Silent video-only output is not an acceptable fallback.
        Assert.DoesNotContain(
            "\"-an\"",
            source);
    }

    [Fact]
    public void StreamingService_ReleasesItsCanonicalMediaLease()
    {
        Assert.Contains(
            "await lease.DisposeAsync()",
            StreamingSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StreamingService_DoesNotUseMediaStdinAsFfmpegControlChannel()
    {
        Assert.DoesNotContain(
            "StandardInput.WriteAsync('q')",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "StandardInput.WriteLine",
            StreamingSource,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(
                    Path.Combine(
                        directory.FullName,
                        ".git")))
            {
                return directory.FullName;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }

    [Fact]
    public void StreamingService_RequestsLiveAlignedCanonicalVideo()
    {
        Assert.Contains(
            "StreamReceiverMediaStartMode.LiveAligned",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "-itsoffset",
            StreamingSource,
            StringComparison.Ordinal);
    }
}
