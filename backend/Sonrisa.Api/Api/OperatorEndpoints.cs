using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Api;

public static class OperatorEndpoints
{
    public static void MapOperatorEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/alerts", GetAlertsAsync);
        api.MapPost("/alerts", CreateAlertAsync);
        api.MapPut("/alerts/{id:int}", UpdateAlertAsync);
        api.MapPatch("/alerts/{id:int}/enabled", SetAlertEnabledAsync);
        api.MapGet("/earthquakes", GetEarthquakesAsync);
        api.MapGet("/deliveries", GetDeliveriesAsync);

        if (app.Environment.IsDevelopment())
        {
            api.MapPost("/demo/earthquake", CreateDemoEarthquakeAsync);
        }
    }

    private static async Task<IResult> GetAlertsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var alerts = await db.Alerts.AsNoTracking().OrderByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        var channels = await db.AlertChannels.AsNoTracking().ToListAsync(cancellationToken);
        var byAlert = channels.GroupBy(item => item.AlertId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Channel).ToArray());
        return Results.Ok(alerts.Select(alert => ToResponse(alert,
            byAlert.GetValueOrDefault(alert.Id) ?? [])).ToArray());
    }

    private static async Task<IResult> CreateAlertAsync(
        AlertWriteRequest request, AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null) return Results.BadRequest(new { error });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var alert = new Alert
        {
            Name = request.Name.Trim(),
            MinimumMagnitude = request.MinimumMagnitude,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
        var selected = request.Channels!.Distinct().ToArray();
        db.AlertChannels.AddRange(selected.Select(channel => new AlertChannel
        {
            AlertId = alert.Id,
            Channel = channel
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/alerts/{alert.Id}", ToResponse(alert, selected));
    }

    private static async Task<IResult> UpdateAlertAsync(
        int id, AlertWriteRequest request, AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null) return Results.BadRequest(new { error });

        var alert = await db.Alerts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (alert is null) return Results.NotFound();

        alert.Name = request.Name.Trim();
        alert.MinimumMagnitude = request.MinimumMagnitude;
        alert.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var current = await db.AlertChannels.Where(item => item.AlertId == id)
            .ToListAsync(cancellationToken);
        var selected = request.Channels!.Distinct().ToHashSet();
        db.AlertChannels.RemoveRange(current.Where(item => !selected.Contains(item.Channel)));
        db.AlertChannels.AddRange(selected.Except(current.Select(item => item.Channel))
            .Select(channel => new AlertChannel { AlertId = id, Channel = channel }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(alert, selected));
    }

    private static async Task<IResult> SetAlertEnabledAsync(
        int id, EnabledRequest request, AppDbContext db, TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var alert = await db.Alerts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (alert is null) return Results.NotFound();
        alert.IsEnabled = request.IsEnabled;
        alert.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        var channels = await db.AlertChannels.AsNoTracking()
            .Where(item => item.AlertId == id)
            .Select(item => item.Channel)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(ToResponse(alert, channels));
    }

    private static async Task<IResult> GetEarthquakesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var earthquakes = await db.EarthquakeEvents.AsNoTracking()
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(earthquakes.Select(item => new EarthquakeResponse(
            item.Id, item.SourceEventId, item.Title, item.Place, item.Magnitude,
            Utc(item.OccurredAtUtc), item.SourceUrl, item.SourceEventId.StartsWith("demo-"))).ToArray());
    }

    private static async Task<IResult> GetDeliveriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var deliveries = await db.Deliveries.AsNoTracking().OrderByDescending(item => item.Id)
            .Take(100).ToListAsync(cancellationToken);
        var eventIds = deliveries.Select(item => item.EarthquakeEventId).Distinct().ToArray();
        var alertIds = deliveries.Select(item => item.AlertId).Distinct().ToArray();
        var earthquakes = await db.EarthquakeEvents.AsNoTracking()
            .Where(item => eventIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var alerts = await db.Alerts.AsNoTracking()
            .Where(item => alertIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return Results.Ok(deliveries.Select(item => new DeliveryResponse(
            item.Id, earthquakes[item.EarthquakeEventId].Title, alerts[item.AlertId].Name,
            item.Channel, item.Status, item.AttemptCount,
            item.LastAttemptAtUtc is null ? null : Utc(item.LastAttemptAtUtc.Value),
            item.LastError)).ToArray());
    }

    private static async Task<IResult> CreateDemoEarthquakeAsync(
        DemoEarthquakeRequest request, AppDbContext db, DeliveryCreationService deliveryCreation,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(request.Magnitude) || request.Magnitude < 2.5)
        {
            return Results.BadRequest(new { error = "Demo magnitude must be at least 2.5." });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var earthquake = new EarthquakeEvent
        {
            SourceEventId = $"demo-{Guid.NewGuid():N}",
            OccurredAtUtc = now,
            FirstSeenAtUtc = now,
            Magnitude = request.Magnitude,
            Place = "Local demo",
            Title = $"Local demo earthquake (M{request.Magnitude.ToString("0.0", CultureInfo.InvariantCulture)})",
            SourceUrl = "local-demo"
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.EarthquakeEvents.Add(earthquake);
        await db.SaveChangesAsync(cancellationToken);
        var deliveryCount = await deliveryCreation.CreatePendingDeliveriesAsync(earthquake.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { earthquakeId = earthquake.Id, deliveryCount });
    }

    private static string? Validate(AlertWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return "Alert name must be between 1 and 100 characters.";
        if (!double.IsFinite(request.MinimumMagnitude) || request.MinimumMagnitude < 2.5)
            return "Minimum magnitude must be at least 2.5, the floor of the selected USGS feed.";
        if (request.Channels is null || request.Channels.Count == 0 ||
            request.Channels.Any(channel => !Enum.IsDefined(channel)))
            return "Select Email, Slack, or both.";
        return null;
    }

    private static AlertResponse ToResponse(Alert alert, IEnumerable<NotificationChannel> channels) =>
        new(alert.Id, alert.Name, alert.MinimumMagnitude, alert.IsEnabled,
            Utc(alert.CreatedAtUtc), Utc(alert.UpdatedAtUtc), channels.Order().ToArray());

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public sealed record AlertWriteRequest(string Name, double MinimumMagnitude, List<NotificationChannel>? Channels);
    public sealed record EnabledRequest(bool IsEnabled);
    public sealed record DemoEarthquakeRequest(double Magnitude);
    public sealed record AlertResponse(int Id, string Name, double MinimumMagnitude, bool IsEnabled,
        DateTime CreatedAtUtc, DateTime UpdatedAtUtc, NotificationChannel[] Channels);
    public sealed record EarthquakeResponse(int Id, string SourceEventId, string Title, string Place,
        double Magnitude, DateTime OccurredAtUtc, string SourceUrl, bool IsDemo);
    public sealed record DeliveryResponse(int Id, string EarthquakeTitle, string AlertName,
        NotificationChannel Channel, DeliveryStatus Status, int AttemptCount,
        DateTime? LastAttemptAtUtc, string? LastError);
}
