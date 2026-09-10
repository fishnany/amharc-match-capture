using AmharcAgent.Core.Models;
using AmharcAgent.Infrastructure.Network;

namespace AmharcAgent.Tests;

internal sealed class FakeWindowsNetworkMutationOperations
    : IWindowsNetworkMutationOperations
{
    public List<FieldNetworkMutationCommand> Applied { get; } = new();

    public FieldNetworkMutationCommandKind? FailOn { get; set; }

    public Task ApplyAsync(
        FieldNetworkMutationCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (FailOn == command.Kind)
            throw new InvalidOperationException($"Simulated failure for {command.Kind}.");

        Applied.Add(command);
        return Task.CompletedTask;
    }
}