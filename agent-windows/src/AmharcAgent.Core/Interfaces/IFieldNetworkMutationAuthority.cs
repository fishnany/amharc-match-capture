using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IFieldNetworkMutationAuthority
{
    Task<FieldNetworkMutationExecutionResult> ExecuteAsync(
        IReadOnlyList<FieldNetworkMutationCommand> commands,
        CancellationToken ct = default);
}