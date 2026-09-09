using System.Collections.Concurrent;
using System.Threading.Channels;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AmharcAgent.Api.Publication;

/// <summary>
/// Coalesces canonical broadcast publication requests.
///
/// Match-aware domain mutations call RequestPublication(matchId) directly.
/// Global singleton state sources such as the match clock and overlay do not
/// carry match identity, so their change events request publication for the
/// currently active match.
///
/// Every emitted renderer message is re-composed as a complete
/// BroadcastPresentationStateV1 by IBroadcastPresentationPublisher.
/// </summary>
public sealed class BroadcastPresentationPublicationHostedService(
    IMatchClockService clock,
    IOverlayService overlay,
    IServiceScopeFactory scopeFactory,
    ILogger<BroadcastPresentationPublicationHostedService> logger)
    : BackgroundService,
      IBroadcastPresentationPublicationScheduler
{
    private const string ActiveMatchRequest =
        "__active_match__";

    private readonly Channel<string> _requests =
        Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    private readonly ConcurrentDictionary<string, byte> _pending =
        new(StringComparer.Ordinal);

    public override Task StartAsync(
        CancellationToken cancellationToken)
    {
        clock.StateChanged += OnClockStateChanged;
        overlay.StateChanged += OnOverlayStateChanged;

        return base.StartAsync(
            cancellationToken);
    }

    public override Task StopAsync(
        CancellationToken cancellationToken)
    {
        clock.StateChanged -= OnClockStateChanged;
        overlay.StateChanged -= OnOverlayStateChanged;

        return base.StopAsync(
            cancellationToken);
    }

    public void RequestPublication(
        string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            matchId);

        Queue(
            matchId);
    }

    private void OnClockStateChanged(
        AmharcAgent.Core.Models.ClockState _)
    {
        Queue(
            ActiveMatchRequest);
    }

    private void OnOverlayStateChanged(
        AmharcAgent.Core.Models.OverlayState _)
    {
        Queue(
            ActiveMatchRequest);
    }

    private void Queue(
        string key)
    {
        if (!_pending.TryAdd(
                key,
                0))
        {
            return;
        }

        if (!_requests.Writer.TryWrite(
                key))
        {
            _pending.TryRemove(
                key,
                out _);
        }
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (
            var key in
            _requests.Reader.ReadAllAsync(
                stoppingToken))
        {
            _pending.TryRemove(
                key,
                out _);

            try
            {
                var matchId =
                    string.Equals(
                        key,
                        ActiveMatchRequest,
                        StringComparison.Ordinal)
                        ? await ResolveActiveMatchIdAsync(
                            stoppingToken)
                        : key;

                if (string.IsNullOrWhiteSpace(
                        matchId))
                {
                    continue;
                }

                using var scope =
                    scopeFactory.CreateScope();

                var publisher =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IBroadcastPresentationPublisher>();

                await publisher.PublishAsync(
                    matchId,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish canonical broadcast presentation state for request {RequestKey}",
                    key);
            }
        }
    }

    private async Task<string?> ResolveActiveMatchIdAsync(
        CancellationToken ct)
    {
        using var scope =
            scopeFactory.CreateScope();

        var matches =
            scope.ServiceProvider
                .GetRequiredService<IMatchRepository>();

        var active =
            await matches.GetActiveMatchAsync(
                ct);

        return active?.MatchId;
    }
}
