using System.Threading.Channels;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using AmharcAgent.Data.Repositories;

namespace AmharcAgent.Api.Publication;

/// <summary>
/// Serialises canonical ClockSnapshot v1 publication requests.
///
/// Authoritative Capture clock-state changes request publication for the
/// operationally live match. Semantic command paths may also request
/// publication explicitly when they already know the canonical match ID.
///
/// Requests are coalesced through a bounded single-reader channel so
/// publication cannot overlap or accumulate an unbounded backlog.
///
/// This service publishes canonical snapshots only. It does not implement
/// Tagger SYNC_CLOCK transport or consumer behaviour.
/// </summary>
public sealed class ClockSnapshotPublicationHostedService(
    IMatchClockService clock,
    IServiceScopeFactory scopeFactory,
    IClockSnapshotPublisher publisher,
    ILogger<ClockSnapshotPublicationHostedService> logger)
    : BackgroundService,
      IClockSnapshotPublicationScheduler
{
    private sealed record PublicationRequest(
        string? MatchId);

    private readonly Channel<PublicationRequest> _publicationRequests =
        Channel.CreateBounded<PublicationRequest>(
            new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode =
                    BoundedChannelFullMode.DropOldest
            });

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        clock.StateChanged += OnClockStateChanged;

        logger.LogInformation(
            "Canonical clock snapshot publication service started");

        return ProcessPublicationRequestsAsync(
            stoppingToken);
    }

    public void RequestPublication(
        string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            matchId);

        _publicationRequests.Writer.TryWrite(
            new PublicationRequest(
                matchId));
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        clock.StateChanged -= OnClockStateChanged;

        _publicationRequests.Writer.TryComplete();

        await base.StopAsync(
            cancellationToken);

        logger.LogInformation(
            "Canonical clock snapshot publication service stopped");
    }

    private void OnClockStateChanged(
        ClockState _)
    {
        _publicationRequests.Writer.TryWrite(
            new PublicationRequest(
                MatchId: null));
    }

    private async Task ProcessPublicationRequestsAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await foreach (
                var request in _publicationRequests.Reader
                    .ReadAllAsync(stoppingToken))
            {
                await PublishAsync(
                    request,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }

    private async Task PublishAsync(
        PublicationRequest request,
        CancellationToken ct)
    {
        try
        {
            var matchId =
                request.MatchId;

            if (string.IsNullOrWhiteSpace(
                matchId))
            {
                using var scope =
                    scopeFactory.CreateScope();

                var matches =
                    scope.ServiceProvider
                        .GetRequiredService<IMatchRepository>();

                var activeMatch =
                    await matches.GetActiveMatchAsync(
                        ct);

                if (activeMatch is null)
                {
                    logger.LogDebug(
                        "Clock state changed but no operationally live match is available for canonical snapshot publication");

                    return;
                }

                matchId =
                    activeMatch.MatchId;
            }

            await publisher.PublishAsync(
                matchId,
                ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Canonical clock snapshot publication failed");
        }
    }
}
