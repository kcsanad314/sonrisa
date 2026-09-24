namespace Sonrisa.Api.Notifications;

public sealed class DeliveryProcessingWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DeliveryProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessSafelyAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessSafelyAsync(stoppingToken);
        }
    }

    private async Task ProcessSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processed = await scope.ServiceProvider.GetRequiredService<DeliveryProcessingService>()
                .ProcessPendingAsync(cancellationToken);
            if (processed > 0)
            {
                logger.LogInformation("Processed {DeliveryCount} pending deliveries.", processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Pending delivery processing failed; it will resume on the next pass.");
        }
    }
}
