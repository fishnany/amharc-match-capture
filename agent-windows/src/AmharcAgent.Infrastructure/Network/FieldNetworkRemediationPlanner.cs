using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Pure planner for bounded AMHARC Capture-network remediation.
/// It never executes PowerShell, netsh, WMI, CIM, or other mutation APIs.
/// </summary>
internal static class FieldNetworkRemediationPlanner
{
    public static FieldNetworkRemediationPlan Plan(
        FieldNetworkProfile profile,
        WindowsFieldNetworkObservation observation)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(observation);

        var candidates = observation.Adapters
            .Where(a => a.IsUp)
            .Where(a =>
                EqualsIgnoreCase(a.Alias, profile.CaptureAdapterAlias) ||
                EqualsIgnoreCase(NormaliseMac(a.MacAddress), NormaliseMac(profile.CaptureAdapterMacAddress)))
            .ToArray();

        if (candidates.Length == 0)
        {
            return Refused(
                observation.ObservedAt,
                "Capture adapter identity could not be positively established.");
        }

        if (candidates.Length > 1)
        {
            return Refused(
                observation.ObservedAt,
                "Capture adapter identity is ambiguous; remediation is unsafe.");
        }

        var adapter = candidates[0];

        var aliasMatches = EqualsIgnoreCase(adapter.Alias, profile.CaptureAdapterAlias);
        var macMatches = EqualsIgnoreCase(
            NormaliseMac(adapter.MacAddress),
            NormaliseMac(profile.CaptureAdapterMacAddress));

        if (!aliasMatches || !macMatches)
        {
            return Refused(
                observation.ObservedAt,
                "Capture adapter requires both canonical alias and hardware MAC identity before remediation.",
                adapter.Alias);
        }

        var actions = new List<FieldNetworkRepairAction>();

        var expectedAddress = adapter.Addresses.FirstOrDefault(a =>
            EqualsIgnoreCase(a.Address, profile.CaptureAddress));

        if (expectedAddress is null ||
            expectedAddress.PrefixLength != profile.CapturePrefixLength ||
            adapter.Addresses.Count != 1)
        {
            actions.Add(new(
                FieldNetworkRepairActionKind.SetCaptureIpv4,
                $"Restore {profile.CaptureAddress}/{profile.CapturePrefixLength} as the sole Capture IPv4 address."));
        }

        if (adapter.Gateways.Any(g => !IsUnspecified(g)))
        {
            actions.Add(new(
                FieldNetworkRepairActionKind.RemoveCaptureGateway,
                "Remove IPv4 gateway configuration from the Capture adapter."));
        }

        var captureRoutePresent = observation.Routes.Any(r =>
            r.InterfaceIndex == adapter.InterfaceIndex &&
            EqualsIgnoreCase(r.DestinationPrefix, profile.CaptureSubnet));

        if (!captureRoutePresent)
        {
            actions.Add(new(
                FieldNetworkRepairActionKind.EnsureCaptureSubnetRoute,
                $"Ensure direct route {profile.CaptureSubnet} uses the Capture adapter."));
        }

        var captureDefaultPresent = observation.Routes.Any(r =>
            r.InterfaceIndex == adapter.InterfaceIndex &&
            EqualsIgnoreCase(r.DestinationPrefix, "0.0.0.0/0"));

        if (captureDefaultPresent)
        {
            actions.Add(new(
                FieldNetworkRepairActionKind.RemoveCaptureDefaultRoute,
                "Remove default IPv4 route from the Capture adapter."));
        }

        var externalDefaultPresent = observation.Routes.Any(r =>
            r.InterfaceIndex != adapter.InterfaceIndex &&
            EqualsIgnoreCase(r.DestinationPrefix, "0.0.0.0/0"));

        if (!externalDefaultPresent)
        {
            return Refused(
                observation.ObservedAt,
                "No external default route exists. AMHARC will not guess or create an Internet gateway.",
                adapter.Alias);
        }

        if (actions.Count == 0)
        {
            return new(
                observation.ObservedAt,
                FieldNetworkRemediationDisposition.NoAction,
                "Canonical Capture network configuration is already satisfied.",
                adapter.Alias,
                Array.Empty<FieldNetworkRepairAction>());
        }

        return new(
            observation.ObservedAt,
            FieldNetworkRemediationDisposition.RepairAvailable,
            "A bounded Capture-network repair plan is available.",
            adapter.Alias,
            actions);
    }

    private static FieldNetworkRemediationPlan Refused(
        DateTimeOffset plannedAt,
        string reason,
        string? adapterName = null) =>
        new(
            plannedAt,
            FieldNetworkRemediationDisposition.Refused,
            reason,
            adapterName,
            Array.Empty<FieldNetworkRepairAction>());

    private static bool EqualsIgnoreCase(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string NormaliseMac(string? value) =>
        (value ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static bool IsUnspecified(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        EqualsIgnoreCase(value, "0.0.0.0");
}