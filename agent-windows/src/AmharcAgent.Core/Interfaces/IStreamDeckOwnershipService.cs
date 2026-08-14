using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IStreamDeckOwnershipService
{
    StreamDeckOwnershipState State { get; }

    IReadOnlyList<string> CompetingProcesses { get; }

    Task<StreamDeckOwnershipState> InspectAsync(
        CancellationToken ct = default);

    Task<StreamDeckOwnershipState> AcquireAsync(
        CancellationToken ct = default);
}
