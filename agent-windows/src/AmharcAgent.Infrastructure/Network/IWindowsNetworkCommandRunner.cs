using System.Diagnostics;

namespace AmharcAgent.Infrastructure.Network;

internal interface IWindowsNetworkCommandRunner
{
    Task RunPowerShellAsync(
        string script,
        CancellationToken ct = default);
}

internal sealed class ProcessWindowsNetworkCommandRunner
    : IWindowsNetworkCommandRunner
{
    public async Task RunPowerShellAsync(
        string script,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        var startInfo =
            new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        process.Start();

        var stdoutTask =
            process.StandardOutput.ReadToEndAsync(ct);

        var stderrTask =
            process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout =
            await stdoutTask;

        var stderr =
            await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Windows network mutation command failed with exit code " +
                $"{process.ExitCode}. stdout='{stdout.Trim()}' " +
                $"stderr='{stderr.Trim()}'.");
        }
    }
}