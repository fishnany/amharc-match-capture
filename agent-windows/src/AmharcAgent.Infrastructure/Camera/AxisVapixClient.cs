using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Camera;

/// <summary>
/// Low-level HTTP wrapper for the AXIS VAPIX API.
/// Targets the AXIS Q6128-E with factory-default credentials (root/pass).
/// </summary>
public class AxisVapixClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<AxisVapixClient> _logger;
    private readonly string _baseUrl;

    public AxisVapixClient(string ipAddress, string username, string password, ILogger<AxisVapixClient> logger)
    {
        _logger = logger;
        _baseUrl = $"http://{ipAddress}";

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(username, password),
            PreAuthenticate = true
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    /// <summary>Fetch device info via VAPIX basic device info CGI.</summary>
    public async Task<VapixDeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/axis-cgi/basicdeviceinfo.cgi";
        _logger.LogDebug("VAPIX GET {Url}", url);
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseDeviceInfo(body);
    }

    /// <summary>Build the RTSP stream URL for this camera (H.264, main stream).</summary>
    public string GetRtspUrl(string? profileName = null)
    {
        var host = new Uri(_baseUrl).Host;
        return profileName is not null
            ? $"rtsp://{host}/axis-media/media.amp?streamprofile={Uri.EscapeDataString(profileName)}"
            : $"rtsp://{host}/axis-media/media.amp?videocodec=h264";
    }

    /// <summary>Continuous PTZ pan/tilt move. Speed range -100 to 100.</summary>
    public async Task PtzContinuousMoveAsync(int panSpeed, int tiltSpeed, CancellationToken ct = default) =>
        await VapixPtzGetAsync($"continuouspantiltmove={panSpeed},{tiltSpeed}", ct);

    /// <summary>Continuous zoom. Speed -100 to 100.</summary>
    public async Task PtzContinuousZoomAsync(int zoomSpeed, CancellationToken ct = default) =>
        await VapixPtzGetAsync($"continuouszoommove={zoomSpeed}", ct);

    /// <summary>Stop all PTZ movement.</summary>
    public async Task PtzStopAsync(CancellationToken ct = default) =>
        await VapixPtzGetAsync("continuouspantiltmove=0,0&continuouszoommove=0", ct);

    /// <summary>Move to absolute pan/tilt/zoom position. AXIS Q6128-E: pan -170..170, tilt -20..90, zoom 1..9999.</summary>
    public async Task PtzMoveAbsoluteAsync(double pan, double tilt, double zoom, CancellationToken ct = default) =>
        await VapixPtzGetAsync($"pan={pan:F1}&tilt={tilt:F1}&zoom={zoom:F0}", ct);

    /// <summary>Return camera to home position.</summary>
    public async Task PtzGoHomeAsync(CancellationToken ct = default) =>
        await VapixPtzGetAsync("move=home", ct);

    /// <summary>Recall a saved preset position by number (1-based).</summary>
    public async Task RecallPresetAsync(int presetNumber, CancellationToken ct = default) =>
        await VapixPtzGetAsync($"gotoserverpresetno={presetNumber}", ct);

    /// <summary>Save current position as a named preset.</summary>
    public async Task SavePresetAsync(int presetNumber, string name, CancellationToken ct = default) =>
        await VapixPtzGetAsync(
            $"setserverpresetno={presetNumber}&setserverpresetname={Uri.EscapeDataString(name)}", ct);

    /// <summary>Retrieve all saved PTZ presets.</summary>
    public async Task<IEnumerable<VapixPreset>> GetPresetsAsync(CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/axis-cgi/com/ptz.cgi?query=presetposall&camera=1";
        var body = await _http.GetStringAsync(url, ct);
        return ParsePresets(body);
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task VapixPtzGetAsync(string queryString, CancellationToken ct)
    {
        var url = $"{_baseUrl}/axis-cgi/com/ptz.cgi?camera=1&{queryString}";
        _logger.LogDebug("VAPIX PTZ {Url}", url);
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("VAPIX PTZ returned {Status} for {Url}", response.StatusCode, url);
    }

    private static VapixDeviceInfo ParseDeviceInfo(string body)
    {
        // VAPIX basicdeviceinfo returns JSON: {"apiVersion":"1.0","data":{"propertyList":{...}}}
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var props = doc.RootElement
            .GetProperty("data")
            .GetProperty("propertyList");

        static string? TryGet(System.Text.Json.JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) ? v.GetString() : null;

        return new VapixDeviceInfo(
            TryGet(props, "Model"),
            TryGet(props, "SerialNumber"),
            TryGet(props, "Version"),
            TryGet(props, "HardwareID"));
    }

    private static IEnumerable<VapixPreset> ParsePresets(string body)
    {
        // Response format: "presetposno=1\r\npresetposname=Home\r\n..."
        var presets = new List<VapixPreset>();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var current = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            var parts = line.Trim().Split('=', 2);
            if (parts.Length == 2) current[parts[0].Trim()] = parts[1].Trim();
            if (current.ContainsKey("presetposno") && current.ContainsKey("presetposname"))
            {
                if (int.TryParse(current["presetposno"], out var num))
                    presets.Add(new VapixPreset(num.ToString(), current["presetposname"], num == 1));
                current.Clear();
            }
        }
        return presets;
    }

    public void Dispose() => _http.Dispose();
}

public record VapixDeviceInfo(string? Model, string? SerialNumber, string? FirmwareVersion, string? MacAddress);
public record VapixPreset(string PresetId, string Name, bool IsHome);
