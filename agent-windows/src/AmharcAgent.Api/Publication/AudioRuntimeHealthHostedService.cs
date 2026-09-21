using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Api.Publication;

/// <summary>
/// Periodically refreshes authoritative audio runtime health outside the
/// readiness request path.
/// </summary>
public sealed class AudioRuntimeHealthHostedService(
    IAudioRuntimeHealthObserver observer,
    ILogger<AudioRuntimeHealthHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan ObservationInterval =
        TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Authoritative audio runtime health observer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await observer.ObserveOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogWarning(
                    "Authoritative audio runtime health observation failed");
            }

            try
            {
                await Task.Delay(
                    ObservationInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "Authoritative audio runtime health observer stopped");
    }
}
