namespace AmharcAgent.Infrastructure.Network;

internal sealed record WindowsNetworkAdapterObservation(
    int InterfaceIndex,
    string Alias,
    string Description,
    string MacAddress,
    bool IsUp,
    IReadOnlyList<WindowsIpv4AddressObservation> Addresses,
    IReadOnlyList<string> Gateways);

internal sealed record WindowsIpv4AddressObservation(
    string Address,
    int PrefixLength);

internal sealed record WindowsRouteObservation(
    int InterfaceIndex,
    string InterfaceAlias,
    string DestinationPrefix,
    string NextHop);

internal sealed record WindowsEndpointObservation(
    string Address,
    int Port,
    bool Reachable,
    string? Detail);

internal sealed record WindowsFieldNetworkObservation(
    DateTimeOffset ObservedAt,
    IReadOnlyList<WindowsNetworkAdapterObservation> Adapters,
    IReadOnlyList<WindowsRouteObservation> Routes,
    WindowsEndpointObservation Camera,
    WindowsEndpointObservation Audio);