using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Converts an already dry-run-verified repair plan into canonical declarative
/// Windows mutation commands. This type does not execute commands.
/// </summary>
internal sealed class FieldNetworkMutationCommandBuilder
{
    public IReadOnlyList<FieldNetworkMutationCommand> Build(
        FieldNetworkProfile profile,
        WindowsFieldNetworkObservation freshObservation,
        FieldNetworkRemediationExecutionResult dryRun)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(freshObservation);
        ArgumentNullException.ThrowIfNull(dryRun);

        if (dryRun.Status != FieldNetworkRemediationExecutionStatus.DryRunVerified ||
            dryRun.ValidatedActions.Count == 0)
            throw new InvalidOperationException("Only a non-empty DryRunVerified result may be converted to mutation commands.");

        var adapters = freshObservation.Adapters
            .Where(a => a.IsUp)
            .Where(a =>
                string.Equals(a.Alias, profile.CaptureAdapterAlias, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormaliseMac(a.MacAddress), NormaliseMac(profile.CaptureAdapterMacAddress), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (adapters.Length != 1)
            throw new InvalidOperationException("Canonical Capture hardware identity cannot be uniquely re-established.");

        var adapter=adapters[0];

        if (!string.Equals(dryRun.CaptureAdapterName, adapter.Alias, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Dry-run result is not bound to the freshly identified Capture adapter.");

        var commands=new List<FieldNetworkMutationCommand>();

        foreach(var action in dryRun.ValidatedActions)
        {
            if (action.Kind == FieldNetworkRepairActionKind.RemoveCaptureGateway &&
                dryRun.ValidatedActions.Any(a =>
                    a.Kind == FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute))
            {
                // GatewayAddresses and the default-route observation can describe the same
                // Windows 0.0.0.0/0 state. Preserve both planner diagnostics, but emit
                // only the canonical default-route mutation to avoid duplicate side effects.
                continue;
            }

            commands.Add(action.Kind switch
            {
                FieldNetworkRepairActionKind.SetCaptureIpv4 =>
                    new(FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4,
                        adapter.InterfaceIndex, profile.CaptureAdapterAlias,
                        profile.CaptureAddress, profile.CapturePrefixLength, null, null),

                FieldNetworkRepairActionKind.RemoveCaptureGateway =>
                    new(FieldNetworkMutationCommandKind.RemoveCaptureGateway,
                        adapter.InterfaceIndex, profile.CaptureAdapterAlias,
                        null, null, null, null),

                FieldNetworkRepairActionKind.EnsureCaptureSubnetRoute =>
                    new(FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute,
                        adapter.InterfaceIndex, profile.CaptureAdapterAlias,
                        null, null, profile.CaptureSubnet, "0.0.0.0"),

                FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute =>
                    new(FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute,
                        adapter.InterfaceIndex, profile.CaptureAdapterAlias,
                        null, null, "0.0.0.0/0", null),

                _ => throw new InvalidOperationException($"Unsupported repair action: {action.Kind}")
            });
        }

        ValidateCanonical(profile, adapter, commands);
        return commands;
    }

    private static void ValidateCanonical(
        FieldNetworkProfile profile,
        WindowsNetworkAdapterObservation adapter,
        IReadOnlyList<FieldNetworkMutationCommand> commands)
    {
        foreach(var command in commands)
        {
            if(command.InterfaceIndex != adapter.InterfaceIndex ||
               !string.Equals(command.AdapterAlias, profile.CaptureAdapterAlias, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mutation command escaped the canonical Capture adapter.");

            switch(command.Kind)
            {
                case FieldNetworkMutationCommandKind.SetCanonicalCaptureIpv4:
                    if(command.Address != profile.CaptureAddress ||
                       command.PrefixLength != profile.CapturePrefixLength ||
                       command.DestinationPrefix is not null ||
                       command.NextHop is not null)
                        throw new InvalidOperationException("Non-canonical Capture IPv4 command rejected.");
                    break;

                case FieldNetworkMutationCommandKind.RemoveCaptureGateway:
                    if(command.Address is not null || command.PrefixLength is not null ||
                       command.DestinationPrefix is not null || command.NextHop is not null)
                        throw new InvalidOperationException("Gateway-removal command contains unexpected parameters.");
                    break;

                case FieldNetworkMutationCommandKind.EnsureCanonicalCaptureSubnetRoute:
                    if(command.DestinationPrefix != profile.CaptureSubnet ||
                       command.NextHop != "0.0.0.0" ||
                       command.Address is not null || command.PrefixLength is not null)
                        throw new InvalidOperationException("Non-canonical Capture subnet route rejected.");
                    break;

                case FieldNetworkMutationCommandKind.RemoveCaptureDefaultRoute:
                    if(command.DestinationPrefix != "0.0.0.0/0" ||
                       command.Address is not null || command.PrefixLength is not null ||
                       command.NextHop is not null)
                        throw new InvalidOperationException("Non-canonical default-route removal rejected.");
                    break;

                default:
                    throw new InvalidOperationException("Unknown mutation command rejected.");
            }
        }
    }

    private static string NormaliseMac(string? value) =>
        (value ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
}
