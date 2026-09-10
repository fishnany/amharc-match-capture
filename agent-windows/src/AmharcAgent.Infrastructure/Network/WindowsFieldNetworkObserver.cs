using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Network;

/// <summary>
/// Authoritative read-only acquisition seam for raw Windows field-network state.
/// This type MUST NOT mutate adapter, address, gateway, route, DNS, metric, or
/// firewall state.
/// </summary>
internal sealed class WindowsFieldNetworkObserver(
    ILogger<WindowsFieldNetworkService> logger)
    : IWindowsFieldNetworkObserver
{
    private static readonly TimeSpan EndpointTimeout =
        TimeSpan.FromSeconds(2);

    public async Task<WindowsFieldNetworkObservation> ObserveAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "AMHARC field-network observation is supported on Windows only.");
        }

        var profile =
            FieldNetworkProfile.Canonical;

        var adapters =
            ReadAdapters();

        var routes =
            await ReadIpv4RoutesAsync(ct);

        var cameraTask =
            ProbeEndpointAsync(
                profile.CameraAddress,
                profile.CameraRtspPort,
                ct);

        var audioTask =
            ProbeEndpointAsync(
                profile.AudioAddress,
                profile.AudioRtspPort,
                ct);

        await Task.WhenAll(cameraTask, audioTask);

        return new WindowsFieldNetworkObservation(
            DateTimeOffset.UtcNow,
            adapters,
            routes,
            await cameraTask,
            await audioTask);
    }
    private static IReadOnlyList<WindowsNetworkAdapterObservation>
        ReadAdapters()
    {
        var result =
            new List<WindowsNetworkAdapterObservation>();

        foreach (var networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            var properties =
                networkInterface.GetIPProperties();

            var ipv4Properties =
                properties.GetIPv4Properties();

            var interfaceIndex =
                ipv4Properties?.Index ?? -1;

            var addresses =
                properties.UnicastAddresses
                    .Where(address =>
                        address.Address.AddressFamily ==
                        AddressFamily.InterNetwork)
                    .Select(address =>
                        new WindowsIpv4AddressObservation(
                            address.Address.ToString(),
                            GetPrefixLength(address)))
                    .ToArray();

            var gateways =
                properties.GatewayAddresses
                    .Where(gateway =>
                        gateway.Address.AddressFamily ==
                        AddressFamily.InterNetwork)
                    .Select(gateway =>
                        gateway.Address.ToString())
                    .ToArray();

            result.Add(
                new WindowsNetworkAdapterObservation(
                    interfaceIndex,
                    networkInterface.Name,
                    networkInterface.Description,
                    FormatMac(
                        networkInterface.GetPhysicalAddress()),
                    networkInterface.OperationalStatus ==
                    OperationalStatus.Up,
                    addresses,
                    gateways));
        }

        return result;
    }

    private static int GetPrefixLength(
        UnicastIPAddressInformation address)
    {
        try
        {
            return address.PrefixLength;
        }
        catch (PlatformNotSupportedException)
        {
            var maskBytes =
                address.IPv4Mask.GetAddressBytes();

            return maskBytes.Sum(
                value =>
                    Convert.ToString(value, 2)
                        .Count(bit => bit == '1'));
        }
    }

    private static string FormatMac(
        PhysicalAddress address)
    {
        var bytes =
            address.GetAddressBytes();

        return string.Join(
            "-",
            bytes.Select(value =>
                value.ToString("X2")));
    }

    private async Task<IReadOnlyList<WindowsRouteObservation>>
        ReadIpv4RoutesAsync(
            CancellationToken ct)
    {
        const string command =
            "Get-NetRoute -AddressFamily IPv4 | " +
            "Select-Object InterfaceIndex,InterfaceAlias," +
            "DestinationPrefix,NextHop | ConvertTo-Json -Compress";

        var startInfo =
            new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        process.Start();

        var stdoutTask =
            process.StandardOutput.ReadToEndAsync(ct);

        var stderrTask =
            process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout =
            await stdoutTask;

        var stderr =
            await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Get-NetRoute failed: " +
                stderr.Trim());
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return Array.Empty<WindowsRouteObservation>();
        }

        try
        {
            using var document =
                JsonDocument.Parse(stdout);

            var routes =
                new List<WindowsRouteObservation>();

            if (document.RootElement.ValueKind ==
                JsonValueKind.Array)
            {
                foreach (var item in
                         document.RootElement.EnumerateArray())
                {
                    routes.Add(ParseRoute(item));
                }
            }
            else if (document.RootElement.ValueKind ==
                     JsonValueKind.Object)
            {
                routes.Add(
                    ParseRoute(document.RootElement));
            }

            return routes;
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Unable to parse Get-NetRoute JSON output.");

            throw new InvalidOperationException(
                "Unable to parse Windows IPv4 route state.",
                ex);
        }
    }

    private static WindowsRouteObservation ParseRoute(
        JsonElement item) =>
        new(
            item.GetProperty("InterfaceIndex").GetInt32(),
            item.TryGetProperty(
                "InterfaceAlias",
                out var alias)
                    ? alias.GetString() ?? string.Empty
                    : string.Empty,
            item.GetProperty(
                "DestinationPrefix").GetString() ??
                string.Empty,
            item.GetProperty(
                "NextHop").GetString() ??
                string.Empty);


    private static async Task<WindowsEndpointObservation>
        ProbeEndpointAsync(
            string address,
            int port,
            CancellationToken ct)
    {
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(
            EndpointTimeout);

        try
        {
            using var client =
                new TcpClient(
                    AddressFamily.InterNetwork);

            await client.ConnectAsync(
                IPAddress.Parse(address),
                port,
                timeout.Token);

            var localEndpoint =
                client.Client.LocalEndPoint as IPEndPoint;

            return new WindowsEndpointObservation(
                address,
                port,
                Reachable: true,
                Detail:
                    localEndpoint is null
                        ? $"TCP {port} reachable."
                        : $"TCP {port} reachable from " +
                          $"{localEndpoint.Address}.");
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return new WindowsEndpointObservation(
                address,
                port,
                Reachable: false,
                Detail:
                    $"TCP {port} timed out.");
        }
        catch (SocketException ex)
        {
            return new WindowsEndpointObservation(
                address,
                port,
                Reachable: false,
                Detail:
                    $"TCP {port} unavailable: " +
                    ex.SocketErrorCode);
        }
    }
}
