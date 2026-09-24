using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Notifications;

public sealed class DeliveryProcessingService(
    AppDbContext db,
    IEnumerable<INotificationSender> senders,
    TimeProvider timeProvider,
    ILogger<DeliveryProcessingService> logger)
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationSender> _senders =
        senders.ToDictionary(sender => sender.Channel);

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingIds = await db.Deliveries.AsNoTracking()
            .Where(item => item.Status == DeliveryStatus.Pending)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            var delivery = await db.Deliveries.SingleAsync(item => item.Id == id, cancellationToken);
            if (delivery.Status != DeliveryStatus.Pending)
            {
                continue;
            }

            delivery.AttemptCount++;
            delivery.LastAttemptAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            delivery.LastError = null;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                if (!_senders.TryGetValue(delivery.Channel, out var sender))
                {
                    throw new InvalidOperationException($"No sender is registered for {delivery.Channel}.");
                }

                var earthquake = await db.EarthquakeEvents.AsNoTracking()
                    .SingleAsync(item => item.Id == delivery.EarthquakeEventId, cancellationToken);
                var alert = await db.Alerts.AsNoTracking()
                    .SingleAsync(item => item.Id == delivery.AlertId, cancellationToken);
                var message = new NotificationMessage(
                    $"Earthquake alert: M{earthquake.Magnitude.ToString("0.0", CultureInfo.InvariantCulture)}",
                    $"{earthquake.Title}\nAlert: {alert.Name}\n{earthquake.SourceUrl}");
                await sender.SendAsync(message, cancellationToken);
                delivery.Status = DeliveryStatus.Sent;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.LastError = exception.Message.Length > 1000
                    ? exception.Message[..1000] : exception.Message;
                logger.LogWarning("Delivery {DeliveryId} on channel {Channel} failed: {Error}",
                    delivery.Id, delivery.Channel, delivery.LastError);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return pendingIds.Count;
    }
}
