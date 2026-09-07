using AmharcAgent.Infrastructure.Runtime;
using FluentAssertions;
using Xunit;

namespace AmharcAgent.Tests;

public class FfmpegRuntimeResolverTests
{
    [Fact]
    public void Resolve_UsesExplicitConfiguredExecutable()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var explicitPath = Path.Combine(root, "custom", "ffmpeg.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(explicitPath)!);
            File.WriteAllText(explicitPath, string.Empty);

            var resolved = FfmpegRuntimeResolver.Resolve(
                explicitPath,
                Path.Combine(root, "app"),
                Path.Combine(root, "machine"));

            resolved.Should().Be(Path.GetFullPath(explicitPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesApplicationManagedRuntimeBeforeMachineRuntime()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var appBase = Path.Combine(root, "app");
            var machineRoot = Path.Combine(root, "machine");
            var appFfmpeg = Path.Combine(appBase, "Runtime", "ffmpeg", "bin", "ffmpeg.exe");
            var machineFfmpeg = Path.Combine(machineRoot, "ffmpeg", "bin", "ffmpeg.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(appFfmpeg)!);
            Directory.CreateDirectory(Path.GetDirectoryName(machineFfmpeg)!);
            File.WriteAllText(appFfmpeg, string.Empty);
            File.WriteAllText(machineFfmpeg, string.Empty);

            var resolved = FfmpegRuntimeResolver.Resolve(
                "ffmpeg.exe",
                appBase,
                machineRoot);

            resolved.Should().Be(Path.GetFullPath(appFfmpeg));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesMachineManagedRuntimeWhenApplicationRuntimeIsAbsent()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var appBase = Path.Combine(root, "app");
            var machineRoot = Path.Combine(root, "machine");
            var machineFfmpeg = Path.Combine(machineRoot, "ffmpeg", "bin", "ffmpeg.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(machineFfmpeg)!);
            File.WriteAllText(machineFfmpeg, string.Empty);

            var resolved = FfmpegRuntimeResolver.Resolve(
                "ffmpeg",
                appBase,
                machineRoot);

            resolved.Should().Be(Path.GetFullPath(machineFfmpeg));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_RejectsMissingExplicitExecutable()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var missing = Path.Combine(root, "missing", "ffmpeg.exe");

            Action act = () => FfmpegRuntimeResolver.Resolve(
                missing,
                Path.Combine(root, "app"),
                Path.Combine(root, "machine"));

            act.Should()
                .Throw<FileNotFoundException>()
                .WithMessage("*Configured AMHARC FFmpeg executable was not found*");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_DoesNotFallBackToWindowsPath()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            Action act = () => FfmpegRuntimeResolver.Resolve(
                "ffmpeg.exe",
                Path.Combine(root, "app"),
                Path.Combine(root, "machine"));

            act.Should()
                .Throw<FileNotFoundException>()
                .WithMessage("*does not fall back to the Windows PATH*");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "amharc-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}