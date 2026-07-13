using System.Net;
using System.Net.NetworkInformation;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Camera;

/// <summary>
/// Scans local subnets for AXIS cameras using parallel HTTP probes.
/// Concurrency is limited to 20 simultaneous probes to avoid flooding the network.
/// </summary>
public class CameraDiscoveryService(ILogger<CameraDiscoveryService> logger) : ICameraDiscoveryService
{
    private const int MaxConcurrency = 20;
    private const int ProbeTimeoutMs = 800;

    public async Task<IEnumerable<DiscoveredCamera>> ScanSubnetAsync(
        string? subnet,
        IProgress<(int Found, int Scanned, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var subnets = string.IsNullOrWhiteSpace(subnet)
            ? DetectLocalSubnets()
            : [subnet];

        var candidates = subnets
            .SelectMany(s => Enumerable.Range(1, 254).Select(i => $"{s}.{i}"))
            .ToList();

        logger.LogInformation("Camera discovery: scanning {Count} addresses across {Subnets} subnet(s)",
            candidates.Count, subnets.Count);

        var found = new System.Collections.Concurrent.ConcurrentBag<DiscoveredCamera>();
        var scanned = 0;
        var semaphore = new SemaphoreSlim(MaxConcurrency);

        var tasks = candidates.Select(async ip =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var camera = await ProbeAddressAsync(ip, ct);
                var s = Interlocked.Increment(ref scanned);
                if (camera is not null)
                {
                    found.Add(camera);
                    logger.LogInformation("Discovered AXIS camera at {Ip} ({Model})", ip, camera.Model);
                }
                progress?.Report((found.Count, s, candidates.Count));
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        logger.LogInformation("Discovery complete: {Found} camera(s) found", found.Count);
        return found;
    }

    private async Task<DiscoveredCamera?> ProbeAddressAsync(string ip, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeoutMs);
        try
        {
            using var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential("root", "pass"),
                PreAuthenticate = true
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(ProbeTimeoutMs) };
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("root:pass"));
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var url = $"http://{ip}/axis-cgi/basicdeviceinfo.cgi";
            var response = await http.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            if (!body.Contains("SerialNumber", StringComparison.OrdinalIgnoreCase)) return null;

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var props = doc.RootElement.GetProperty("data").GetProperty("propertyList");

            static string? TryGet(System.Text.Json.JsonElement el, string key) =>
                el.TryGetProperty(key, out var v) ? v.GetString() : null;

            return new DiscoveredCamera(
                ip,
                TryGet(props, "HardwareID"),
                TryGet(props, "Model"),
                TryGet(props, "SerialNumber"),
                TryGet(props, "Version"));
        }
        catch { return null; }
    }

    private static List<string> DetectLocalSubnets()
    {
        var subnets = new HashSet<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                var parts = addr.Address.ToString().Split('.');
                if (parts.Length == 4)
                    subnets.Add($"{parts[0]}.{parts[1]}.{parts[2]}");
            }
        }
        return subnets.Count > 0 ? [.. subnets] : ["192.168.1"];
    }
}
