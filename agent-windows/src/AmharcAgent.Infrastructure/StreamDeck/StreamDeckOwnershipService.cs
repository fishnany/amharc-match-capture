using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.StreamDeck;

public sealed class StreamDeckOwnershipService(
    AgentSettings settings,
    IStreamDeckProcessManager processManager,
    ILogger<StreamDeckOwnershipService> logger)
    : IStreamDeckOwnershipService
{
    private readonly List<string> _competingProcesses = new();

    public StreamDeckOwnershipState State { get; private set; } =
        StreamDeckOwnershipState.Unavailable;

    public IReadOnlyList<string> CompetingProcesses =>
        _competingProcesses;

    public Task<StreamDeckOwnershipState> InspectAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _competingProcesses.Clear();

        try
        {
            var competingProcesses =
                processManager.FindCompetingProcesses();

            foreach (var process in competingProcesses)
            {
                _competingProcesses.Add(
                    $"{process.ProcessName} ({process.ProcessId})");
            }

            if (_competingProcesses.Count > 0 &&
                settings.StreamDeck.ExclusiveOwnership)
            {
                State = StreamDeckOwnershipState.Conflicted;

                logger.LogWarning(
                    "Stream Deck ownership conflict detected: {Processes}",
                    string.Join(", ", _competingProcesses));
            }
            else
            {
                State = StreamDeckOwnershipState.Controlled;

                logger.LogInformation(
                    "No competing Stream Deck controller process detected");
            }
        }
        catch (Exception ex)
        {
            State = StreamDeckOwnershipState.Error;

            logger.LogError(
                ex,
                "Failed to inspect Stream Deck ownership state");
        }

        return Task.FromResult(State);
    }

    public async Task<StreamDeckOwnershipState> AcquireAsync(
        CancellationToken ct = default)
    {
        var state = await InspectAsync(ct);

        if (state != StreamDeckOwnershipState.Conflicted)
            return state;

        if (!settings.StreamDeck.ExclusiveOwnership)
            return state;

        if (!settings.StreamDeck.CloseCompetingSoftwareOnStartup)
            return state;

        try
        {
            var competingProcesses =
                processManager.FindCompetingProcesses();

            foreach (var process in competingProcesses)
            {
                try
                {
                    logger.LogInformation(
                        "Closing competing Stream Deck process {ProcessName} ({ProcessId})",
                        process.ProcessName,
                        process.ProcessId);

                    await processManager.CloseProcessAsync(
                        process.ProcessId,
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to close competing Stream Deck process {ProcessId}",
                        process.ProcessId);
                }
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(500),
                ct);

            return await InspectAsync(ct);
        }
        catch (Exception ex)
        {
            State = StreamDeckOwnershipState.Error;

            logger.LogError(
                ex,
                "Failed to acquire exclusive Stream Deck ownership");

            return State;
        }
    }
}