using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Network;

internal static class FieldNetworkStateEvaluator
{
    public static FieldNetworkState Evaluate(
        FieldNetworkProfile profile,
        WindowsFieldNetworkObservation observation)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(observation);

        var captureAdapter =
            observation.Adapters
                .Where(adapter => adapter.IsUp)
                .OrderByDescending(adapter =>
                    string.Equals(
                        adapter.Alias,
                        profile.CaptureAdapterAlias,
                        StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(adapter =>
                    string.Equals(
                        NormaliseMac(adapter.MacAddress),
                        NormaliseMac(profile.CaptureAdapterMacAddress),
                        StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(adapter =>
                    string.Equals(
                        adapter.Alias,
                        profile.CaptureAdapterAlias,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        NormaliseMac(adapter.MacAddress),
                        NormaliseMac(profile.CaptureAdapterMacAddress),
                        StringComparison.OrdinalIgnoreCase));

        if (captureAdapter is null)
        {
            return new FieldNetworkState(
                observation.ObservedAt,
                profile.CaptureSubnet,
                profile.CaptureAddress,
                CaptureAdapterName: null,
                ObservedCaptureAddress: null,
                ObservedPrefixLength: null,
                CaptureAdapterPresent: false,
                CaptureAddressCorrect: false,
                CaptureGatewayAbsent: false,
                CaptureSubnetRoutePresent: false,
                DefaultRouteOutsideCaptureAdapter: HasDefaultRouteOutside(
                    observation.Routes,
                    captureInterfaceIndex: null),
                Camera: ToEndpointState(
                    "Axis Camera",
                    observation.Camera),
                Audio: ToEndpointState(
                    "RODE Audio",
                    observation.Audio));
        }

        var expectedAddress =
            captureAdapter.Addresses.FirstOrDefault(address =>
                string.Equals(
                    address.Address,
                    profile.CaptureAddress,
                    StringComparison.OrdinalIgnoreCase));

        var representativeAddress =
            expectedAddress ??
            captureAdapter.Addresses.FirstOrDefault();

        var captureAddressCorrect =
            expectedAddress is not null &&
            expectedAddress.PrefixLength == profile.CapturePrefixLength;

        var captureGatewayAbsent =
            captureAdapter.Gateways.Count == 0 ||
            captureAdapter.Gateways.All(IsUnspecifiedGateway);

        var captureSubnetRoutePresent =
            observation.Routes.Any(route =>
                route.InterfaceIndex == captureAdapter.InterfaceIndex &&
                string.Equals(
                    route.DestinationPrefix,
                    profile.CaptureSubnet,
                    StringComparison.OrdinalIgnoreCase));

        var defaultRouteOutsideCaptureAdapter =
            !observation.Routes.Any(route =>
                route.InterfaceIndex == captureAdapter.InterfaceIndex &&
                IsDefaultRoute(route.DestinationPrefix)) &&
            HasDefaultRouteOutside(
                observation.Routes,
                captureAdapter.InterfaceIndex);

        return new FieldNetworkState(
            observation.ObservedAt,
            profile.CaptureSubnet,
            profile.CaptureAddress,
            captureAdapter.Alias,
            representativeAddress?.Address,
            representativeAddress?.PrefixLength,
            CaptureAdapterPresent: true,
            CaptureAddressCorrect: captureAddressCorrect,
            CaptureGatewayAbsent: captureGatewayAbsent,
            CaptureSubnetRoutePresent: captureSubnetRoutePresent,
            DefaultRouteOutsideCaptureAdapter: defaultRouteOutsideCaptureAdapter,
            Camera: ToEndpointState(
                "Axis Camera",
                observation.Camera),
            Audio: ToEndpointState(
                "RODE Audio",
                observation.Audio));
    }

    private static bool HasDefaultRouteOutside(
        IReadOnlyList<WindowsRouteObservation> routes,
        int? captureInterfaceIndex) =>
        routes.Any(route =>
            IsDefaultRoute(route.DestinationPrefix) &&
            (!captureInterfaceIndex.HasValue ||
             route.InterfaceIndex != captureInterfaceIndex.Value));

    private static bool IsDefaultRoute(string destinationPrefix) =>
        string.Equals(
            destinationPrefix,
            "0.0.0.0/0",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsUnspecifiedGateway(string gateway) =>
        string.IsNullOrWhiteSpace(gateway) ||
        string.Equals(
            gateway,
            "0.0.0.0",
            StringComparison.OrdinalIgnoreCase);

    private static string NormaliseMac(string? value) =>
        (value ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static FieldNetworkEndpointState ToEndpointState(
        string name,
        WindowsEndpointObservation endpoint) =>
        new(
            name,
            endpoint.Address,
            endpoint.Reachable,
            endpoint.Detail);
}