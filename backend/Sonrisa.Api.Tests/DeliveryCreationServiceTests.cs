using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Tests;

public sealed class DeliveryCreationServiceTests
{
    private static readonly DateTime AlertCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CreatesPendingRowsForSelectedChannels(bool email, bool slack)
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var channels = SelectedChannels(email, slack);
        var (earthquake, _) = await SeedAsync(fixture.Db, channels);

        var created = await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id);
        var deliveries = await fixture.Db.Deliveries.AsNoTracking().ToListAsync();

        Assert.Equal(channels.Length, created);
        Assert.Equal(
            channels.OrderBy(channel => channel),
            deliveries.Select(delivery => delivery.Channel).OrderBy(channel => channel));
        Assert.All(deliveries, delivery =>
        {
            Assert.Equal(DeliveryStatus.Pending, delivery.Status);
            Assert.Equal(0, delivery.AttemptCount);
            Assert.Null(delivery.LastAttemptAtUtc);
            Assert.Null(delivery.LastError);
        });
    }

    [Fact]
    public async Task CreatesNothingWithoutSelectedChannels()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, _) = await SeedAsync(fixture.Db);

        var created = await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id);

        Assert.Equal(0, created);
        Assert.Empty(await fixture.Db.Deliveries.ToListAsync());
    }

    [Fact]
    public async Task ReprocessingDoesNotDuplicateOrResetAnExistingDelivery()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, _) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        var service = NewService(fixture.Db);

        Assert.Equal(1, await service.CreatePendingDeliveriesAsync(earthquake.Id));
        var delivery = await fixture.Db.Deliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Sent;
        delivery.AttemptCount = 1;
        delivery.LastAttemptAtUtc = AlertCreatedAt.AddDays(2);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(0, await service.CreatePendingDeliveriesAsync(earthquake.Id));
        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.Deliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Sent, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(AlertCreatedAt.AddDays(2), persisted.LastAttemptAtUtc);
        Assert.Equal(1, await fixture.Db.Deliveries.CountAsync());
    }

    [Fact]
    public async Task ReprocessingInNewDbContextPreservesFailedDeliveryState()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, _) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        Assert.Equal(1, await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id));

        var delivery = await fixture.Db.Deliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Failed;
        delivery.AttemptCount = 2;
        delivery.LastAttemptAtUtc = AlertCreatedAt.AddDays(2);
        delivery.LastError = "Fixture timeout";
        await fixture.Db.SaveChangesAsync();

        await using var freshDb = fixture.CreateDbContext();
        Assert.Equal(0, await NewService(freshDb).CreatePendingDeliveriesAsync(earthquake.Id));

        var persisted = await freshDb.Deliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Failed, persisted.Status);
        Assert.Equal(2, persisted.AttemptCount);
        Assert.Equal(AlertCreatedAt.AddDays(2), persisted.LastAttemptAtUtc);
        Assert.Equal("Fixture timeout", persisted.LastError);
        Assert.Equal(1, await freshDb.Deliveries.CountAsync());
    }

    [Fact]
    public async Task CreatesOneDeliveryForEachDistinctEarthquake()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (firstEarthquake, _) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        var secondEarthquake = NewEarthquake(firstEarthquake.OccurredAtUtc.AddMinutes(1));
        fixture.Db.EarthquakeEvents.Add(secondEarthquake);
        await fixture.Db.SaveChangesAsync();
        var service = NewService(fixture.Db);

        Assert.Equal(1, await service.CreatePendingDeliveriesAsync(firstEarthquake.Id));
        Assert.Equal(1, await service.CreatePendingDeliveriesAsync(secondEarthquake.Id));

        var eventIds = await fixture.Db.Deliveries
            .Select(delivery => delivery.EarthquakeEventId)
            .OrderBy(id => id)
            .ToListAsync();
        Assert.Equal(new[] { firstEarthquake.Id, secondEarthquake.Id }.OrderBy(id => id), eventIds);
    }

    [Fact]
    public async Task CreatesSeparateDeliveriesForOverlappingAlerts()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, firstAlert) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        var secondAlert = NewAlert("second alert");
        fixture.Db.Alerts.Add(secondAlert);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.AlertChannels.Add(new AlertChannel
        {
            AlertId = secondAlert.Id,
            Channel = NotificationChannel.Email
        });
        await fixture.Db.SaveChangesAsync();

        var created = await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id);
        var alertIds = await fixture.Db.Deliveries
            .Select(delivery => delivery.AlertId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(2, created);
        Assert.Equal(new[] { firstAlert.Id, secondAlert.Id }.OrderBy(id => id), alertIds);
    }

    [Fact]
    public async Task SkipsDisabledAndNonmatchingAlerts()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, matchingAlert) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        var disabled = NewAlert("disabled");
        disabled.IsEnabled = false;
        var tooHigh = NewAlert("too high");
        tooHigh.MinimumMagnitude = 6.0;
        var createdAfterEvent = NewAlert("created after event");
        createdAfterEvent.CreatedAtUtc = earthquake.OccurredAtUtc;
        fixture.Db.Alerts.AddRange(disabled, tooHigh, createdAfterEvent);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.AlertChannels.AddRange(new[] { disabled, tooHigh, createdAfterEvent }
            .Select(alert => new AlertChannel
            {
                AlertId = alert.Id,
                Channel = NotificationChannel.Email
            }));
        await fixture.Db.SaveChangesAsync();

        var created = await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id);
        var delivery = await fixture.Db.Deliveries.SingleAsync();

        Assert.Equal(1, created);
        Assert.Equal(matchingAlert.Id, delivery.AlertId);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateEventAlertChannel()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var (earthquake, alert) = await SeedAsync(fixture.Db, NotificationChannel.Email);
        Assert.Equal(1, await NewService(fixture.Db).CreatePendingDeliveriesAsync(earthquake.Id));

        fixture.Db.Deliveries.Add(new Delivery
        {
            EarthquakeEventId = earthquake.Id,
            AlertId = alert.Id,
            Channel = NotificationChannel.Email,
            Status = DeliveryStatus.Pending
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    private static DeliveryCreationService NewService(AppDbContext db) =>
        new(db, new EarthquakeMatcher());

    private static NotificationChannel[] SelectedChannels(bool email, bool slack)
    {
        var channels = new List<NotificationChannel>();
        if (email) channels.Add(NotificationChannel.Email);
        if (slack) channels.Add(NotificationChannel.Slack);
        return channels.ToArray();
    }

    private static Alert NewAlert(string name = "alert") => new()
    {
        Name = name,
        MinimumMagnitude = 4.5,
        IsEnabled = true,
        CreatedAtUtc = AlertCreatedAt,
        UpdatedAtUtc = AlertCreatedAt
    };

    private static async Task<(EarthquakeEvent Earthquake, Alert Alert)> SeedAsync(
        AppDbContext db,
        params NotificationChannel[] channels)
    {
        var earthquake = NewEarthquake(AlertCreatedAt.AddDays(1));
        var alert = NewAlert();
        db.EarthquakeEvents.Add(earthquake);
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();

        db.AlertChannels.AddRange(channels.Select(channel => new AlertChannel
        {
            AlertId = alert.Id,
            Channel = channel
        }));
        await db.SaveChangesAsync();

        return (earthquake, alert);
    }

    private static EarthquakeEvent NewEarthquake(DateTime occurredAtUtc) => new()
    {
        SourceEventId = Guid.NewGuid().ToString("N"),
        OccurredAtUtc = occurredAtUtc,
        FirstSeenAtUtc = occurredAtUtc,
        Magnitude = 5.0,
        Place = "Fixture location",
        Title = "Fixture earthquake",
        SourceUrl = "https://example.test/earthquake"
    };

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }

        private SqliteFixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public AppDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new SqliteFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
