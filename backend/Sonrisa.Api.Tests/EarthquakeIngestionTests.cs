using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;
using Sonrisa.Api.Ingestion;

namespace Sonrisa.Api.Tests;

public sealed class EarthquakeIngestionTests
{
    private static readonly DateTime BaselineTime = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FirstSuccessfulPollStoresEventsWithoutDeliveries()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAlertAsync();
        var feed = new FakeFeed(Snapshot(Event("first", BaselineTime.AddMinutes(-1))));

        var result = await Service(fixture.Db, feed).PollAsync();

        Assert.Equal((1, 0), result);
        Assert.Equal(BaselineTime, (await fixture.Db.IngestionStates.SingleAsync()).BaselineAtUtc);
        Assert.Equal("first", (await fixture.Db.EarthquakeEvents.SingleAsync()).SourceEventId);
        Assert.Empty(await fixture.Db.Deliveries.ToListAsync());
    }

    [Fact]
    public async Task EmptyFirstFeedStillEstablishesBaselineAndRestartUsesIt()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAlertAsync();
        await Service(fixture.Db, new FakeFeed(Snapshot())).PollAsync();

        await using var freshDb = fixture.NewContext();
        var result = await Service(freshDb,
            new FakeFeed(Snapshot(Event("new", BaselineTime.AddMinutes(1))))).PollAsync();

        Assert.Equal((1, 1), result);
        Assert.Single(await freshDb.IngestionStates.ToListAsync());
        Assert.Single(await freshDb.Deliveries.ToListAsync());
    }

    [Fact]
    public async Task RepeatedAndRevisedIdsDoNotCreateAnotherEventOrDelivery()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAlertAsync();
        await Service(fixture.Db, new FakeFeed(Snapshot())).PollAsync();
        var original = Event("same", BaselineTime.AddMinutes(1));
        Assert.Equal((1, 1), await Service(fixture.Db, new FakeFeed(Snapshot(original))).PollAsync());

        var revised = Event("same", BaselineTime.AddMinutes(2), magnitude: 6.5);
        Assert.Equal((0, 0), await Service(fixture.Db, new FakeFeed(Snapshot(revised))).PollAsync());

        Assert.Equal(1, await fixture.Db.EarthquakeEvents.CountAsync());
        Assert.Equal(1, await fixture.Db.Deliveries.CountAsync());
        Assert.Equal(5.0, (await fixture.Db.EarthquakeEvents.SingleAsync()).Magnitude);
    }

    [Fact]
    public async Task LaterPollOnlyMatchesNewEventsAfterPersistedBaseline()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAlertAsync();
        var old = Event("old", BaselineTime.AddMinutes(-1));
        await Service(fixture.Db, new FakeFeed(Snapshot(old))).PollAsync();

        var delayed = Event("delayed", BaselineTime.AddMinutes(-2));
        var equal = Event("equal", BaselineTime);
        var fresh = Event("fresh", BaselineTime.AddMinutes(1));
        var result = await Service(fixture.Db,
            new FakeFeed(Snapshot(old, delayed, equal, fresh))).PollAsync();

        Assert.Equal((3, 1), result);
        Assert.Equal(4, await fixture.Db.EarthquakeEvents.CountAsync());
        var delivery = await fixture.Db.Deliveries.SingleAsync();
        Assert.Equal("fresh", (await fixture.Db.EarthquakeEvents.SingleAsync(
            item => item.Id == delivery.EarthquakeEventId)).SourceEventId);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
    }

    [Fact]
    public async Task FailedInitialFetchLeavesBaselineUnset()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Service(fixture.Db, new FakeFeed(new HttpRequestException("unavailable"))).PollAsync());
        Assert.Empty(await fixture.Db.IngestionStates.ToListAsync());

        await fixture.AddAlertAsync();
        var result = await Service(fixture.Db,
            new FakeFeed(Snapshot(Event("first", BaselineTime.AddMinutes(1))))).PollAsync();
        Assert.Equal((1, 0), result);
    }

    [Fact]
    public async Task DeliveryFailureRollsBackNewEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAlertAsync();
        await Service(fixture.Db, new FakeFeed(Snapshot())).PollAsync();
        await fixture.Db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailDelivery BEFORE INSERT ON Deliveries BEGIN SELECT RAISE(ABORT, 'failure'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() => Service(fixture.Db,
            new FakeFeed(Snapshot(Event("new", BaselineTime.AddMinutes(1))))).PollAsync());

        await using var freshDb = fixture.NewContext();
        Assert.Empty(await freshDb.EarthquakeEvents.ToListAsync());
        Assert.Empty(await freshDb.Deliveries.ToListAsync());
        Assert.Single(await freshDb.IngestionStates.ToListAsync());
    }

    [Fact]
    public void ParserSkipsBadFeatureAndUsesOptionalTextFallbacks()
    {
        var json = """
            {"type":"FeatureCollection","features":[
              {"id":"bad","properties":{"type":"earthquake","mag":null,"time":1000,"url":"https://example.test/bad"}},
              {"id":"good","properties":{"type":"earthquake","mag":5.0,"time":1000,"url":"https://example.test/good"}}
            ]}
            """;

        var earthquakes = UsgsFeedParser.Parse(json, NullLogger.Instance);

        var earthquake = Assert.Single(earthquakes);
        Assert.Equal("good", earthquake.SourceEventId);
        Assert.Equal("Unknown location", earthquake.Place);
        Assert.Equal("Earthquake good", earthquake.Title);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":{}}")]
    public async Task InvalidWholeFeedDoesNotInitialize(string json)
    {
        await using var fixture = await Fixture.CreateAsync();
        var feed = new FakeFeed(new FormatException("Invalid feed"));
        Assert.Throws<FormatException>(() => UsgsFeedParser.Parse(json, NullLogger.Instance));

        await Assert.ThrowsAsync<FormatException>(() => Service(fixture.Db, feed).PollAsync());
        Assert.Empty(await fixture.Db.IngestionStates.ToListAsync());
        Assert.Empty(await fixture.Db.EarthquakeEvents.ToListAsync());
    }

    private static EarthquakeIngestionService Service(AppDbContext db, IEarthquakeFeed feed) =>
        new(db, feed, new DeliveryCreationService(db, new EarthquakeMatcher()), new FixedTimeProvider());

    private static EarthquakeEvent Event(string id, DateTime occurred, double magnitude = 5.0) => new()
    {
        SourceEventId = id,
        OccurredAtUtc = occurred,
        Magnitude = magnitude,
        Place = "Fixture location",
        Title = "Fixture earthquake",
        SourceUrl = $"https://example.test/{id}"
    };

    private static IReadOnlyList<EarthquakeEvent> Snapshot(params EarthquakeEvent[] events) => events;

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(BaselineTime);
    }

    private sealed class FakeFeed : IEarthquakeFeed
    {
        private readonly IReadOnlyList<EarthquakeEvent>? _events;
        private readonly Exception? _exception;
        public FakeFeed(IReadOnlyList<EarthquakeEvent> events) => _events = events;
        public FakeFeed(Exception exception) => _exception = exception;
        public Task<IReadOnlyList<EarthquakeEvent>> FetchAsync(CancellationToken cancellationToken) =>
            _exception is null ? Task.FromResult(_events!) : Task.FromException<IReadOnlyList<EarthquakeEvent>>(_exception);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }
        private Fixture(SqliteConnection connection, AppDbContext db) => (_connection, Db) = (connection, db);
        public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }
        public async Task AddAlertAsync()
        {
            var alert = new Alert
            {
                Name = "M4.5+",
                MinimumMagnitude = 4.5,
                IsEnabled = true,
                CreatedAtUtc = BaselineTime.AddDays(-1),
                UpdatedAtUtc = BaselineTime.AddDays(-1)
            };
            Db.Alerts.Add(alert);
            await Db.SaveChangesAsync();
            Db.AlertChannels.Add(new AlertChannel { AlertId = alert.Id, Channel = NotificationChannel.Email });
            await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
