using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Ingestion;

public sealed class EarthquakeIngestionService(
    AppDbContext db,
    IEarthquakeFeed feed,
    DeliveryCreationService deliveries,
    TimeProvider timeProvider)
{
    public async Task<(int Events, int Deliveries)> PollAsync(CancellationToken cancellationToken = default)
    {
        // Record the snapshot's start before network I/O. Fetch and parse before opening a DB transaction.
        var pollStartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var feedEvents = await feed.FetchAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var baseline = await db.IngestionStates.SingleOrDefaultAsync(cancellationToken);
        var isFirstPoll = baseline is null;
        if (isFirstPoll)
        {
            baseline = new IngestionState { Id = 1, BaselineAtUtc = pollStartedAtUtc };
            db.IngestionStates.Add(baseline);
        }

        var sourceIds = feedEvents.Select(item => item.SourceEventId).Distinct().ToArray();
        var existingIds = await db.EarthquakeEvents
            .Where(item => sourceIds.Contains(item.SourceEventId))
            .Select(item => item.SourceEventId)
            .ToListAsync(cancellationToken);
        var seenIds = existingIds.ToHashSet(StringComparer.Ordinal);
        var inserted = new List<EarthquakeEvent>();

        foreach (var earthquake in feedEvents)
        {
            if (!seenIds.Add(earthquake.SourceEventId))
            {
                continue;
            }

            earthquake.FirstSeenAtUtc = pollStartedAtUtc;
            db.EarthquakeEvents.Add(earthquake);
            inserted.Add(earthquake);
        }

        await db.SaveChangesAsync(cancellationToken);

        var deliveryCount = 0;
        if (!isFirstPoll)
        {
            foreach (var earthquake in inserted.Where(item => item.OccurredAtUtc > baseline!.BaselineAtUtc))
            {
                deliveryCount += await deliveries.CreatePendingDeliveriesAsync(earthquake.Id, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return (inserted.Count, deliveryCount);
    }
}
