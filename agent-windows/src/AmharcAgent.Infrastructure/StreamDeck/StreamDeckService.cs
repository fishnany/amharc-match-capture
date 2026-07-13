using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StreamDeckSharp;

namespace AmharcAgent.Infrastructure.StreamDeck;

/// <summary>
/// Controls the Elgato Stream Deck via StreamDeckSharp HID.
/// Renders coloured button labels and maps key presses to AMHARC events.
/// </summary>
public class StreamDeckService : IStreamDeckService, IAsyncDisposable
{
    private readonly ILogger<StreamDeckService> _logger;
    private IStreamDeck? _device;
    private StreamDeckProfile? _activeProfile;
    private readonly Dictionary<int, StreamDeckButton> _buttonMap = new();
    private readonly HashSet<int> _activeButtons = new();
    private bool _running;

    public bool IsConnected => _device is not null && _running;
    public string? DeviceName { get; private set; }
    public string? ActiveProfileId => _activeProfile?.ProfileId;

    public event Action<int, StreamDeckButton>? ButtonPressed;
    public event Action<string>? Connected;
    public event Action? Disconnected;

    public StreamDeckService(ILogger<StreamDeckService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _ = Task.Run(() => ConnectLoop(ct), ct);
        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _running = false;
        CloseDevice();
        return Task.CompletedTask;
    }

    public async Task LoadProfileAsync(StreamDeckProfile profile, CancellationToken ct = default)
    {
        _activeProfile = profile;
        _buttonMap.Clear();
        foreach (var btn in profile.Buttons)
            _buttonMap[btn.ButtonNumber] = btn;

        if (_device is not null)
        {
            foreach (var btn in profile.Buttons)
                await RenderButtonAsync(btn, false);
        }
        _logger.LogInformation("Loaded Stream Deck profile: {Name}", profile.Name);
    }

    public async Task SetButtonStateAsync(int buttonNumber, bool active, CancellationToken ct = default)
    {
        if (_device is null || !_buttonMap.TryGetValue(buttonNumber, out var btn)) return;
        if (active) _activeButtons.Add(buttonNumber);
        else _activeButtons.Remove(buttonNumber);
        await RenderButtonAsync(btn, active);
    }

    public async Task SetButtonLabelAsync(int buttonNumber, string label, CancellationToken ct = default)
    {
        if (_device is null || !_buttonMap.TryGetValue(buttonNumber, out var btn)) return;
        var updated = new StreamDeckButton
        {
            ButtonNumber = btn.ButtonNumber, Label = label, Icon = btn.Icon,
            Colour = btn.Colour, EventType = btn.EventType, Team = btn.Team,
            ScoreEffect = btn.ScoreEffect, OverlayEffect = btn.OverlayEffect,
            ClipRequest = btn.ClipRequest, Enabled = btn.Enabled
        };
        _buttonMap[buttonNumber] = updated;
        await RenderButtonAsync(updated, _activeButtons.Contains(buttonNumber));
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task ConnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _device = StreamDeck.OpenDevice();
                DeviceName = "Elgato Stream Deck";
                _running = true;
                _device.KeyStateChanged += OnKeyStateChanged;
                _logger.LogInformation("Stream Deck connected: {Name}", DeviceName);
                Connected?.Invoke(DeviceName);

                if (_activeProfile is not null)
                    await LoadProfileAsync(_activeProfile, ct);

                // Wait while device is connected
                while (_running && !ct.IsCancellationRequested)
                    await Task.Delay(1000, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug("Stream Deck not found, retrying in 2s: {Msg}", ex.Message);
                CloseDevice();
                await Task.Delay(2000, ct);
            }
        }
    }

    private void OnKeyStateChanged(object? sender, KeyEventArgs e)
    {
        if (!e.IsDown) return;
        if (_buttonMap.TryGetValue(e.Key, out var btn) && btn.Enabled)
        {
            _logger.LogInformation("Stream Deck button {Key} pressed: {Label}", e.Key, btn.Label);
            ButtonPressed?.Invoke(e.Key, btn);
        }
    }

    private async Task RenderButtonAsync(StreamDeckButton btn, bool active)
    {
        if (_device is null) return;
        try
        {
            // Parse colour from hex string or defaults
            var (r, g, b) = ParseColour(btn.Colour, btn.Team, active);
            var key = OpenMacroBoard.SDK.KeyBitmap.Create.FromRgb(r, g, b);
            _device.SetKeyBitmap(btn.ButtonNumber, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render button {Num}", btn.ButtonNumber);
        }
        await Task.CompletedTask;
    }

    private static (byte R, byte G, byte B) ParseColour(string? hex, ButtonTeam? team, bool active)
    {
        if (active) return (255, 255, 255); // white highlight when active
        if (hex is not null && hex.StartsWith('#') && hex.Length == 7)
        {
            return (
                Convert.ToByte(hex[1..3], 16),
                Convert.ToByte(hex[3..5], 16),
                Convert.ToByte(hex[5..7], 16));
        }
        return team switch
        {
            ButtonTeam.Home => (28, 133, 81),   // AMHARC Green
            ButtonTeam.Away => (182, 220, 70),  // AMHARC Lime
            _ => (40, 40, 40)                   // dark grey
        };
    }

    private void CloseDevice()
    {
        if (_device is null) return;
        try
        {
            _device.KeyStateChanged -= OnKeyStateChanged;
            _device.ClearKeys();
            _device.Dispose();
        }
        catch { /* ignore */ }
        _device = null;
        _running = false;
        DeviceName = null;
        Disconnected?.Invoke();
    }

    public async ValueTask DisposeAsync() { await StopAsync(); }
}
