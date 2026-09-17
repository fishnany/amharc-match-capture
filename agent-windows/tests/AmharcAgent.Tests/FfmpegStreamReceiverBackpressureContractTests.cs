using Xunit;
using System.Text.RegularExpressions;

namespace AmharcAgent.Tests;

public sealed class FfmpegStreamReceiverBackpressureContractTests
{
    private static readonly string Source =
        File.ReadAllText(FindSourceFile());

    [Fact]
    public void LossIntolerantConsumer_UsesBoundedWaitMode()
    {
        Assert.Contains(
            "lossIntolerant",
            Source,
            StringComparison.Ordinal);

        Assert.Contains(
            "BoundedChannelFullMode.Wait",
            Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LossIntolerantDispatch_AwaitsChannelCapacity()
    {
        Assert.Matches(
            new Regex(
                @"if\s*\(\s*consumer\.LossIntolerant\s*\).*?" +
                @"await\s+consumer\.Channel\.Writer\.WriteAsync\s*\(",
                RegexOptions.Singleline),
            Source);
    }

    [Fact]
    public void LossIntolerantDispatch_DoesNotConvertTemporaryFullQueueIntoMediaLossFault()
    {
        Assert.DoesNotContain(
            "Canonical media consumer could not accept ordered media without loss.",
            Source,
            StringComparison.Ordinal);

        Assert.DoesNotMatch(
            new Regex(
                @"if\s*\(\s*!.*?TryWrite\s*\(.*?\)\s*\).*?" +
                @"TryComplete\s*\(\s*new\s+InvalidOperationException",
                RegexOptions.Singleline),
            Source);
    }

    [Fact]
    public void PreviewDispatch_RemainsNonBlocking()
    {
        Assert.Matches(
            new Regex(
                @"else\s*\{\s*" +
                @"consumer\.Channel\.Writer\.TryWrite\s*\(",
                RegexOptions.Singleline),
            Source);
    }

    [Fact]
    public void BackpressureWait_DoesNotOccurInsideMediaDispatchLock()
    {
        var lockStart =
            Source.IndexOf(
                "lock (_mediaDispatchLock)",
                StringComparison.Ordinal);

        Assert.True(
            lockStart >= 0,
            "Expected media dispatch lock was not found.");

        var consumerLoop =
            Source.IndexOf(
                "foreach (var entry in consumers)",
                lockStart,
                StringComparison.Ordinal);

        Assert.True(
            consumerLoop > lockStart,
            "Expected consumer dispatch loop was not found.");

        var lockRegion =
            Source.Substring(
                lockStart,
                consumerLoop - lockStart);

        Assert.DoesNotContain(
            "WriteAsync(",
            lockRegion,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DetachedLossIntolerantLease_DoesNotFaultCanonicalReceiver()
    {
        Assert.Matches(
            new Regex(
                @"catch\s*\(\s*ChannelClosedException\s*\)\s*" +
                @"when\s*\(\s*!_consumers\.ContainsKey\(entry\.Key\)\s*\)",
                RegexOptions.Singleline),
            Source);
    }

    private static string FindSourceFile()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate =
                Path.Combine(
                    directory.FullName,
                    "agent-windows",
                    "src",
                    "AmharcAgent.Infrastructure",
                    "Media",
                    "FfmpegStreamReceiver.cs");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Unable to locate FfmpegStreamReceiver.cs from the test runtime directory.");
    }
}
