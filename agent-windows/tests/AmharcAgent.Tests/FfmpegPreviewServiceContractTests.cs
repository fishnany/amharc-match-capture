using System.Reflection;
using Xunit;
using AmharcAgent.Infrastructure.Preview;
using FluentAssertions;

namespace AmharcAgent.Tests;

public sealed class FfmpegPreviewServiceContractTests
{
    [Fact]
    public void PreviewArguments_AreLowLatencyVideoOnlyMjpeg()
    {
        var method =
            GetPrivateStatic("BuildPreviewArguments");

        const string runtimeUri =
            "rtsp://preview-user:preview-secret@192.168.1.135:554/axis-media/media.amp?videocodec=h264";

        var arguments =
            (string)method.Invoke(
                null,
                [runtimeUri])!;

        arguments.Should().Contain("-rtsp_transport tcp");
        arguments.Should().Contain("-fflags nobuffer");
        arguments.Should().Contain("-flags low_delay");
        arguments.Should().Contain("-map 0:v:0");
        arguments.Should().Contain("-an");
        arguments.Should().Contain("-f mpjpeg");
        arguments.Should().Contain("-boundary_tag amharcframe");
        arguments.Should().Contain("pipe:1");
        arguments.Should().NotContain("-c:v copy");
        arguments.Should().NotContain("192.168.1.136");
    }

    [Fact]
    public void PreviewArguments_DoNotIntroduceRecordingOrAudioOutputs()
    {
        var method =
            GetPrivateStatic("BuildPreviewArguments");

        var arguments =
            (string)method.Invoke(
                null,
                ["rtsp://camera/live"])!;

        arguments.Should().NotContain(".mkv");
        arguments.Should().NotContain("segment");
        arguments.Should().NotContain("-map 1:a:0");
        arguments.Should().NotContain("-c:a");
    }

    [Fact]
    public void PreviewCredentialRedaction_RemovesRuntimeSecret()
    {
        var method =
            GetPrivateStatic("RedactRtspCredentials");

        const string secret =
            "synthetic-preview-secret";

        var raw =
            $"rtsp://preview-user:{secret}@192.168.1.135:554/axis-media/media.amp";

        var redacted =
            (string)method.Invoke(
                null,
                [raw])!;

        redacted.Should().NotContain(secret);
        redacted.Should().NotContain("preview-user");
        redacted.Should().Contain(
            "rtsp://***:***@192.168.1.135:554/");
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