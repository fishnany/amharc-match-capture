namespace AmharcAgent.Infrastructure.Runtime;

/// <summary>
/// Resolves the FFmpeg executable used by AMHARC Capture.
///
/// Resolution is deliberately deterministic:
/// 1. An explicit configured path, when it identifies an existing file.
/// 2. The runtime packaged beside the AMHARC application.
/// 3. The machine-level AMHARC managed runtime.
///
/// Windows PATH resolution is intentionally not used.
/// </summary>
public static class FfmpegRuntimeResolver
{
    public static string Resolve(
        string? configuredPath,
        string? applicationBaseDirectory = null,
        string? machineRuntimeRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) &&
            !IsBareExecutableName(configuredPath))
        {
            var explicitPath =
                Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(
                        configuredPath));

            if (!File.Exists(explicitPath))
            {
                throw new FileNotFoundException(
                    $"Configured AMHARC FFmpeg executable was not found: {explicitPath}",
                    explicitPath);
            }

            return explicitPath;
        }

        var baseDirectory =
            string.IsNullOrWhiteSpace(applicationBaseDirectory)
                ? AppContext.BaseDirectory
                : applicationBaseDirectory;

        var machineRoot =
            string.IsNullOrWhiteSpace(machineRuntimeRoot)
                ? @"C:\AMHARC\Runtime"
                : machineRuntimeRoot;

        var candidates = new[]
        {
            Path.Combine(
                baseDirectory,
                "Runtime",
                "ffmpeg",
                "bin",
                "ffmpeg.exe"),

            Path.Combine(
                machineRoot,
                "ffmpeg",
                "bin",
                "ffmpeg.exe")
        };

        foreach (var candidate in candidates)
        {
            var fullPath =
                Path.GetFullPath(candidate);

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException(
            "AMHARC FFmpeg runtime was not found. " +
            "Configure AmharcAgent:FfmpegPath with an explicit AMHARC-managed executable " +
            "or install FFmpeg under the managed Runtime\\ffmpeg\\bin directory. " +
            "AMHARC does not fall back to the Windows PATH.");
    }

    private static bool IsBareExecutableName(
        string path)
    {
        var trimmed =
            path.Trim();

        return string.Equals(
                   trimmed,
                   "ffmpeg",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   trimmed,
                   "ffmpeg.exe",
                   StringComparison.OrdinalIgnoreCase);
    }
}