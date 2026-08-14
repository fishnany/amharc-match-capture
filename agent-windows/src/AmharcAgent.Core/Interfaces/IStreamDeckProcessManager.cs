namespace AmharcAgent.Core.Interfaces;

public record StreamDeckProcessInfo(
    int ProcessId,
    string ProcessName);

public interface IStreamDeckProcessManager
{
    IReadOnlyList<StreamDeckProcessInfo>
        FindCompetingProcesses();

    Task CloseProcessAsync(
        int processId,
        CancellationToken ct = default);
}