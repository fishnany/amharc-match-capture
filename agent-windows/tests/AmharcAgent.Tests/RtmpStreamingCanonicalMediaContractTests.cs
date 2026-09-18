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
    public void StreamingService_DoesNotContainDirectRtspIngress()
    {
        Assert.DoesNotContain(
            "-rtsp_transport",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "rtsp://camera",
            StreamingSource,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "GetAuthenticatedStreamUrlAsync",
            StreamingSource,
            StringComparison.Ordinal);
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
    public void StreamingService_DoesNotClaimAuthoritativeAudio()
    {
        Assert.Contains(
            "\"-an\"",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "-c:a",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "IAudioCredentialProvider",
            StreamingSource,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "IRecordingAudioSourceResolver",
            StreamingSource,
            StringComparison.Ordinal);
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
}
