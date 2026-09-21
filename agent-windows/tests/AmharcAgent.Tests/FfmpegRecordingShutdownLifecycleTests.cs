using AmharcAgent.Infrastructure.Recording;
using Xunit;

namespace AmharcAgent.Tests;

/// <summary>
/// MR-16D regression guards for Recording container finalisation.
/// These guards protect the production shutdown ordering established by
/// MR16D-REC-FINALISE-02 and the R11 A/B evidence.
/// </summary>
public sealed class FfmpegRecordingShutdownLifecycleTests
{
    [Fact]
    public void RecordingLaunch_UsesDedicatedWindowsProcessGroupBoundary()
    {
        var source = ReadProcessControlSource();

        Assert.Contains(
            "CreateNewProcessGroup = 0x00000200",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "GenerateConsoleCtrlEvent(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "CtrlBreakEvent = 1",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StopRecording_ClosesCanonicalPumpBeforeGracefulTerminationRequest()
    {
        var stop = ReadStopRecordingMethod();

        var stopPump = stop.IndexOf(
            "await StopMediaPumpAsync();",
            StringComparison.Ordinal);
        var graceful = stop.IndexOf(
            "_processControl.RequestGracefulTermination(process.Id);",
            StringComparison.Ordinal);

        Assert.True(stopPump >= 0);
        Assert.True(graceful > stopPump);
    }

    [Fact]
    public void StopRecording_CompleteRequiresBoundedGracefulExit()
    {
        var stop = ReadStopRecordingMethod();

        var gracefulWait = stop.IndexOf(
            "process.WaitForExit(5_000)",
            StringComparison.Ordinal);
        var complete = stop.IndexOf(
            "SetState(RecordingState.Complete)",
            StringComparison.Ordinal);

        Assert.True(gracefulWait >= 0);
        Assert.True(complete > gracefulWait);
        Assert.DoesNotContain(
            "process.WaitForExit(10_000)",
            stop,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StopRecording_GracefulTimeoutFallsBackToKillAndError()
    {
        var stop = ReadStopRecordingMethod();

        Assert.Contains(
            "FFmpeg did not exit within the bounded graceful termination window.",
            stop,
            StringComparison.Ordinal);
        Assert.Contains(
            "process.Kill(entireProcessTree: true);",
            stop,
            StringComparison.Ordinal);
        Assert.Contains(
            "await process.WaitForExitAsync(CancellationToken.None);",
            stop,
            StringComparison.Ordinal);
        Assert.Contains(
            "SetState(RecordingState.Error)",
            stop,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StopRecording_GracefulSignalFailureCannotPersistComplete()
    {
        var stop = ReadStopRecordingMethod();

        var signalFailure = stop.IndexOf(
            "Unable to request graceful FFmpeg process-group termination",
            StringComparison.Ordinal);
        var shutdownFailure = stop.IndexOf(
            "if (shutdownError is not null)",
            StringComparison.Ordinal);
        var complete = stop.IndexOf(
            "SetState(RecordingState.Complete)",
            StringComparison.Ordinal);

        Assert.True(signalFailure >= 0);
        Assert.True(shutdownFailure > signalFailure);
        Assert.True(complete > shutdownFailure);
    }

    [Fact]
    public void StopRecording_PumpFailureCannotBypassProcessTermination()
    {
        var stop = ReadStopRecordingMethod();

        var pumpCatch = stop.IndexOf(
            "Error stopping canonical recording media pump",
            StringComparison.Ordinal);
        var graceful = stop.IndexOf(
            "_processControl.RequestGracefulTermination(process.Id);",
            StringComparison.Ordinal);
        var pumpFailure = stop.IndexOf(
            "Canonical recording media pump failed during shutdown.",
            StringComparison.Ordinal);

        Assert.True(pumpCatch >= 0);
        Assert.True(graceful > pumpCatch);
        Assert.True(pumpFailure > graceful);
    }

    [Fact]
    public void StopRecording_ForcedKillCanNeverBeSuccessfulCompletePath()
    {
        var stop = ReadStopRecordingMethod();

        var kill = stop.IndexOf(
            "process.Kill(entireProcessTree: true);",
            StringComparison.Ordinal);
        var shutdownFailure = stop.IndexOf(
            "if (shutdownError is not null)",
            kill,
            StringComparison.Ordinal);
        var complete = stop.IndexOf(
            "SetState(RecordingState.Complete)",
            StringComparison.Ordinal);

        Assert.True(kill >= 0);
        Assert.True(shutdownFailure > kill);
        Assert.True(complete > shutdownFailure);
    }

    private static string ReadStopRecordingMethod()
    {
        var source = ReadRecordingServiceSource();
        var start = source.IndexOf(
            "public async Task StopRecordingAsync(",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "public async Task<string> RemuxToMp4Async(",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        return source[start..end];
    }

    private static string ReadRecordingServiceSource()
    {
        return File.ReadAllText(
            Path.Combine(
                TestRepository.FindRoot(),
                "agent-windows",
                "src",
                "AmharcAgent.Infrastructure",
                "Recording",
                "FfmpegRecordingService.cs"));
    }

    private static string ReadProcessControlSource()
    {
        return File.ReadAllText(
            Path.Combine(
                TestRepository.FindRoot(),
                "agent-windows",
                "src",
                "AmharcAgent.Infrastructure",
                "Recording",
                "WindowsRecordingProcessControl.cs"));
    }

    
}
