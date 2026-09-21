using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Executes only canonical, typed AMHARC field-network mutation commands.
/// The operating-system boundary is injected so behaviour can be fully tested
/// without touching the live machine.
/// </summary>
internal sealed class FieldNetworkMutationAuthority(
    IWindowsNetworkMutationOperations operations)
    : IFieldNetworkMutationAuthority
{
    public async Task<FieldNetworkMutationExecutionResult> ExecuteAsync(
        IReadOnlyList<FieldNetworkMutationCommand> commands,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return new(
                DateTimeOffset.UtcNow,
                FieldNetworkMutationExecutionStatus.NoAction,
                "No mutation commands supplied.",
                Array.Empty<FieldNetworkMutationActionResult>());
        }

        var profile = FieldNetworkProfile.Canonical;

        foreach (var command in commands)
        {
            ValidateCanonicalCommand(profile, command);
        }

        var results = new List<FieldNetworkMutationActionResult>();

        foreach (var command in commands)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await operations.ApplyAsync(command, ct);
                results.Add(new(
                    command.Kind,
                    true,
                    "Canonical mutation operation applied."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                results.Add(new(
                    command.Kind,
                    false,
                    ex.Message));

                return new(
                    DateTimeOffset.UtcNow,
                    FieldNetworkMutationExecutionStatus.Failed,
                    $"Mutation authority stopped after failure in {command.Kind}.",
                    results);
            }
        }

        return new(
            DateTimeOffset.UtcNow,
            FieldNetworkMutationExecutionStatus.Applied,
            "All canonical mutation operations completed.",
            results);
    }

    private static void ValidateCanonicalCommand(
        FieldNetworkProfile profile,
        FieldNetworkMutationCommand command)
    {
        if (!string.Equals(
                command.AdapterAlias,
                profile.CaptureAdapterAlias,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Mutation command targets a non-canonical adapter alias.");
        }

        if (command.InterfaceIndex < 0)
        {
            throw new InvalidOperationException(
                "Mutation command contains an invalid interface index.");
        }

        switch (command.Kind)
        {
            case FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4:
                if (command.Address != profile.CaptureAddress ||
                    command.PrefixLength != profile.CapturePrefixLength ||
                    command.DestinationPrefix is not null ||
                    command.NextHop is not null)
                {
                    throw new InvalidOperationException(
                        "Non-canonical Capture IPv4 mutation rejected.");
                }
                break;

            case FieldNetworkMutationCommandKind.RemoveCaptureGateway:
                if (command.Address is not null ||
                    command.PrefixLength is not null ||
                    command.DestinationPrefix is not null ||
                    command.NextHop is not null)
                {
                    throw new InvalidOperationException(
                        "Gateway-removal mutation contains unexpected parameters.");
                }
                break;

            case FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute:
                if (command.DestinationPrefix != profile.CaptureSubnet ||
                    command.NextHop != "0.0.0.0" ||
                    command.Address is not null ||
                    command.PrefixLength is not null)
                {
                    throw new InvalidOperationException(
                        "Non-canonical Capture subnet-route mutation rejected.");
                }
                break;

            case FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute:
                if (command.DestinationPrefix != "0.0.0.0/0" ||
                    command.Address is not null ||
                    command.PrefixLength is not null ||
                    command.NextHop is not null)
                {
                    throw new InvalidOperationException(
                        "Non-canonical Capture default-route mutation rejected.");
                }
                break;

            default:
                throw new InvalidOperationException(
                    "Unknown mutation command rejected.");
        }
    }
}