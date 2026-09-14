using System.Reflection;
using Xunit;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Infrastructure.Preview;
using FluentAssertions;

namespace AmharcAgent.Tests;

public sealed class FfmpegPreviewServiceContractTests
{
    [Fact]
    public void PreviewArguments_ConsumeCanonicalMpegTsAndProduceMjpeg()
    {
        var method =
            GetPrivateStatic(
                "BuildPreviewArguments");

        var arguments =
            (string)method.Invoke(
                null,
                null)!;

        arguments.Should()
            .Contain("-fflags nobuffer");

        arguments.Should()
            .Contain("-flags low_delay");

        arguments.Should()
            .Contain("-f mpegts");

        arguments.Should()
            .Contain("-i pipe:0");

        arguments.Should()
            .Contain("-map 0:v:0");

        arguments.Should()
            .Contain("-an");

        arguments.Should()
            .Contain("-f mpjpeg");

        arguments.Should()
            .Contain(
                "-boundary_tag amharcframe");

        arguments.Should()
            .Contain("pipe:1");

        arguments.Should()
            .NotContain("rtsp://");

        arguments.Should()
            .NotContain(
                "-rtsp_transport");

        arguments.Should()
            .NotContain(
                "192.168.1.135");

        arguments.Should()
            .NotContain(
                "192.168.1.136");
    }

    [Fact]
    public void PreviewArguments_DoNotIntroduceRecordingOrAudioOutputs()
    {
        var method =
            GetPrivateStatic(
                "BuildPreviewArguments");

        var arguments =
            (string)method.Invoke(
                null,
                null)!;

        arguments.Should()
            .NotContain(".mkv");

        arguments.Should()
            .NotContain("segment");

        arguments.Should()
            .NotContain("-map 1:a:0");

        arguments.Should()
            .NotContain("-c:a");
    }

    [Fact]
    public void PreviewConstructor_DependsOnCanonicalMediaSourceNotCameraAdapter()
    {
        var constructor =
            typeof(FfmpegMjpegPreviewService)
                .GetConstructors()
                .Single();

        var parameterTypes =
            constructor
                .GetParameters()
                .Select(parameter =>
                    parameter.ParameterType)
                .ToArray();

        parameterTypes.Should()
            .Contain(
                typeof(
                    IStreamReceiverMediaSource));

        parameterTypes.Should()
            .NotContain(
                typeof(ICameraAdapter));
    }

    [Fact]
    public void PreviewType_DoesNotExposeCredentialOrRtspMembers()
    {
        typeof(FfmpegMjpegPreviewService)
            .GetMembers(
                BindingFlags.Public |
                BindingFlags.Instance)
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

    private static MethodInfo GetPrivateStatic(
        string name) =>
        typeof(FfmpegMjpegPreviewService)
            .GetMethod(
                name,
                BindingFlags.NonPublic |
                BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Unable to locate {name}.");
}