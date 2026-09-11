using System.Reflection;
using AmharcAgent.Infrastructure.Recording;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class FfmpegRecordingDualInputContractTests
{
    [Fact]
    public void RecordingMap_WithAudio_UsesAuthoritativeSecondInput()
    {
        var method = GetPrivateStatic("BuildRecordingMapArguments");
        var value = Assert.IsType<string>(method.Invoke(null, new object[] { true }));

        Assert.Equal("-map 0:v:0 -map 1:a:0", value);
        Assert.DoesNotContain("0:a?", value);
    }

    [Fact]
    public void RecordingMap_WithoutAudio_UsesCameraVideoOnly()
    {
        var method = GetPrivateStatic("BuildRecordingMapArguments");
        var value = Assert.IsType<string>(method.Invoke(null, new object[] { false }));

        Assert.Equal("-map 0:v:0", value);
    }

    [Fact]
    public void RecordingAudioArguments_WithAudio_CopyAuthoritativeAudio()
    {
        var method = GetPrivateStatic("BuildRecordingAudioArguments");
        var value = Assert.IsType<string>(method.Invoke(null, new object[] { true }));

        Assert.Equal("-c:a copy", value);
    }

    [Fact]
    public void RecordingAudioArguments_WithoutAudio_DisableAudio()
    {
        var method = GetPrivateStatic("BuildRecordingAudioArguments");
        var value = Assert.IsType<string>(method.Invoke(null, new object[] { false }));

        Assert.Equal("-an", value);
    }

    private static MethodInfo GetPrivateStatic(string name)
    {
        return typeof(FfmpegRecordingService).GetMethod(
                   name,
                   BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException($"Method {name} was not found.");
    }
}