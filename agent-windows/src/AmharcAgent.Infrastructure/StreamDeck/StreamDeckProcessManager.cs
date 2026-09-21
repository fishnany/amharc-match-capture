using System.Diagnostics;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.StreamDeck;

public sealed class StreamDeckProcessManager
    : IStreamDeckProcessManager
{
    public IReadOnlyList<StreamDeckProcessInfo>
        FindCompetingProcesses()
    {
        var result =
            new List<StreamDeckProcessInfo>();

        var processes =
            Process.GetProcessesByName("StreamDeck");

        foreach (var process in processes)
        {
            try
            {
                result.Add(
                    new StreamDeckProcessInfo(
                        process.Id,
                        process.ProcessName));
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    public async Task CloseProcessAsync(
        int processId,
        CancellationToken ct = default)
    {
        using var process =
            Process.GetProcessById(processId);

        process.Kill(
            entireProcessTree: true);

        await process.WaitForExitAsync(ct);
    }
}
