using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Alerts;

public sealed class DeliveryCreationService(AppDbContext db, EarthquakeMatcher matcher)
{
    public async Task<int> CreatePendingDeliveriesAsync(
        int earthquakeEventId,
        CancellationToken cancellationToken = default)
    {
        var earthquake = await db.EarthquakeEvents
            .AsNoTracking()
            .SingleAsync(item => item.Id == earthquakeEventId, cancellationToken);

        var alerts = await db.Alerts
            .AsNoTracking()
            .Where(item => item.IsEnabled)
            .ToListAsync(cancellationToken);

        var matchingAlertIds = alerts
            .Where(alert => matcher.Matches(earthquake, alert))
            .Select(alert => alert.Id)
            .ToArray();

        if (matchingAlertIds.Length == 0)
        {
            return 0;
        }

        var selectedChannels = await db.AlertChannels
            .AsNoTracking()
            .Where(item => matchingAlertIds.Contains(item.AlertId))
            .ToListAsync(cancellationToken);

        if (selectedChannels.Count == 0)
        {
            return 0;
        }

        var existingDeliveries = await db.Deliveries
            .AsNoTracking()
            .Where(item => item.EarthquakeEventId == earthquakeEventId)
            .Select(item => new { item.AlertId, item.Channel })
            .ToListAsync(cancellationToken);

        var existingKeys = existingDeliveries
            .Select(item => (item.AlertId, item.Channel))
            .ToHashSet();

        var created = 0;

        foreach (var selection in selectedChannels)
        {
            if (!existingKeys.Add((selection.AlertId, selection.Channel)))
            {
                continue;
            }

            db.Deliveries.Add(new Delivery
            {
                EarthquakeEventId = earthquakeEventId,
                AlertId = selection.AlertId,
                Channel = selection.Channel,
                Status = DeliveryStatus.Pending,
                AttemptCount = 0
            });

            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
