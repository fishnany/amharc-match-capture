using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.StreamDeck;

/// <summary>
/// Bridges physical Stream Deck button presses into semantic AMHARC commands.
/// Creates a fresh DI scope for each command so scoped match/data services are
/// never injected directly into the singleton hardware listener.
/// </summary>
public sealed class StreamDeckCommandBridge
{
    private readonly IStreamDeckService _streamDeck;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StreamDeckCommandBridge> _logger;
    private bool _started;

    public StreamDeckCommandBridge(
        IStreamDeckService streamDeck,
        IServiceScopeFactory scopeFactory,
        ILogger<StreamDeckCommandBridge> logger)
    {
        _streamDeck = streamDeck;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
            return;

        _streamDeck.ButtonPressed += OnButtonPressed;
        _started = true;

        _logger.LogInformation(
            "Stream Deck command bridge started");
    }

    public void Stop()
    {
        if (!_started)
            return;

        _streamDeck.ButtonPressed -= OnButtonPressed;
        _started = false;

        _logger.LogInformation(
            "Stream Deck command bridge stopped");
    }

    private void OnButtonPressed(
        int buttonNumber,
        StreamDeckButton button)
    {
        if (string.IsNullOrWhiteSpace(button.CommandId))
        {
            _logger.LogWarning(
                "Stream Deck button {ButtonNumber} ({Label}) has no CommandId; press ignored",
                buttonNumber,
                button.Label);

            return;
        }

        _ = DispatchAsync(
            buttonNumber,
            button);
    }

    private async Task DispatchAsync(
        int buttonNumber,
        StreamDeckButton button)
    {
        try
        {
            using var scope =
                _scopeFactory.CreateScope();

            var dispatcher =
                scope.ServiceProvider
                    .GetRequiredService<IAmharcCommandDispatcher>();

            var command = new AmharcCommand(
                CommandId: button.CommandId!,
                Source: EventSource.StreamDeck);

            await dispatcher.DispatchAsync(command);

            _logger.LogInformation(
                "Stream Deck button {ButtonNumber} dispatched AMHARC command {CommandId}",
                buttonNumber,
                button.CommandId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to dispatch Stream Deck button {ButtonNumber} command {CommandId}",
                buttonNumber,
                button.CommandId);
        }
    }
}
