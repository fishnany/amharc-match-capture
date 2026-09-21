using System.Net.NetworkInformation;

namespace AmharcAgent.Infrastructure.Network;

internal sealed record WindowsNetworkAdapterIdentity(
    int InterfaceIndex,
    string Alias,
    string Description,
    string MacAddress,
    bool IsUp);

internal interface IWindowsNetworkAdapterIdentityResolver
{
    WindowsNetworkAdapterIdentity? ResolveByInterfaceIndex(
        int interfaceIndex);
}

internal sealed class WindowsNetworkAdapterIdentityResolver
    : IWindowsNetworkAdapterIdentityResolver
{
    public WindowsNetworkAdapterIdentity? ResolveByInterfaceIndex(
        int interfaceIndex)
    {
        foreach (var networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties =
                networkInterface.GetIPProperties();

            var ipv4 =
                properties.GetIPv4Properties();

            if (ipv4?.Index != interfaceIndex)
                continue;

            return new(
                interfaceIndex,
                networkInterface.Name,
                networkInterface.Description,
                FormatMac(networkInterface.GetPhysicalAddress()),
                networkInterface.OperationalStatus ==
                    OperationalStatus.Up);
        }

        return null;
    }

    private static string FormatMac(
        PhysicalAddress address) =>
        string.Join(
            "-",
            address.GetAddressBytes()
                .Select(value => value.ToString("X2")));
}