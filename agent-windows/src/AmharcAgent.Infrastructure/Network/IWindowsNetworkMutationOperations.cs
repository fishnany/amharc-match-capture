using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

internal interface IWindowsNetworkMutationOperations
{
    Task ApplyAsync(
        FieldNetworkMutationCommand command,
        CancellationToken ct = default);
}