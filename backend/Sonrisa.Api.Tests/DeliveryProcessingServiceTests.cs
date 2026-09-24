using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;
using Sonrisa.Api.Notifications;

namespace Sonrisa.Api.Tests;

public sealed class DeliveryProcessingServiceTests
{
    private static readonly DateTime AttemptTime = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendsEachPendingChannelAndRecordsSuccessfulAttempt()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(NotificationChannel.Email, NotificationChannel.Slack);
        var email = new FakeSender(NotificationChannel.Email);
        var slack = new FakeSender(NotificationChannel.Slack);

        var processed = await Service(fixture.Db, email, slack).ProcessPendingAsync();

        Assert.Equal(2, processed);
        Assert.Single(email.Messages);
        Assert.Single(slack.Messages);
        Assert.Contains("M5.0", email.Messages[0].Subject);
        Assert.Contains("Fixture earthquake", slack.Messages[0].Body);
        Assert.All(await fixture.Db.Deliveries.ToListAsync(), delivery =>
        {
            Assert.Equal(DeliveryStatus.Sent, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
            Assert.Equal(AttemptTime, delivery.LastAttemptAtUtc);
            Assert.Null(delivery.LastError);
        });
    }

    [Fact]
    public async Task FailedSenderDoesNotStopOtherPendingDeliveryAndIsNotRetried()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(NotificationChannel.Email, NotificationChannel.Slack);
        var email = new FakeSender(NotificationChannel.Email, _ => throw new InvalidOperationException("SMTP unavailable"));
        var slack = new FakeSender(NotificationChannel.Slack);
        var service = Service(fixture.Db, email, slack);

        Assert.Equal(2, await service.ProcessPendingAsync());
        Assert.Equal(0, await service.ProcessPendingAsync());

        var failed = await fixture.Db.Deliveries.SingleAsync(item => item.Channel == NotificationChannel.Email);
        var sent = await fixture.Db.Deliveries.SingleAsync(item => item.Channel == NotificationChannel.Slack);
        Assert.Equal(DeliveryStatus.Failed, failed.Status);
        Assert.Equal("SMTP unavailable", failed.LastError);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Equal(AttemptTime, failed.LastAttemptAtUtc);
        Assert.Equal(DeliveryStatus.Sent, sent.Status);
        Assert.Equal(1, sent.AttemptCount);
        Assert.Single(email.Messages);
        Assert.Single(slack.Messages);
    }

    [Fact]
    public async Task PersistsAttemptBeforeCallingSender()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(NotificationChannel.Email);
        var email = new FakeSender(NotificationChannel.Email, async _ =>
        {
            await using var freshDb = fixture.NewContext();
            var duringSend = await freshDb.Deliveries.SingleAsync();
            Assert.Equal(DeliveryStatus.Pending, duringSend.Status);
            Assert.Equal(1, duringSend.AttemptCount);
            Assert.Equal(AttemptTime, duringSend.LastAttemptAtUtc);
        });

        await Service(fixture.Db, email).ProcessPendingAsync();

        Assert.Equal(DeliveryStatus.Sent, (await fixture.Db.Deliveries.SingleAsync()).Status);
    }

    [Fact]
    public async Task MissingSenderFailsOnlyItsDelivery()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(NotificationChannel.Email, NotificationChannel.Slack);
        var email = new FakeSender(NotificationChannel.Email);

        await Service(fixture.Db, email).ProcessPendingAsync();

        Assert.Equal(DeliveryStatus.Sent, (await fixture.Db.Deliveries.SingleAsync(
            item => item.Channel == NotificationChannel.Email)).Status);
        var slack = await fixture.Db.Deliveries.SingleAsync(item => item.Channel == NotificationChannel.Slack);
        Assert.Equal(DeliveryStatus.Failed, slack.Status);
        Assert.Contains("No sender is registered", slack.LastError);
    }

    [Fact]
    public async Task ExistingSentAndFailedRowsAreSkipped()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(NotificationChannel.Email, NotificationChannel.Slack);
        var rows = await fixture.Db.Deliveries.OrderBy(item => item.Id).ToListAsync();
        rows[0].Status = DeliveryStatus.Sent;
        rows[0].AttemptCount = 1;
        rows[1].Status = DeliveryStatus.Failed;
        rows[1].AttemptCount = 1;
        rows[1].LastError = "Previous failure";
        await fixture.Db.SaveChangesAsync();
        var email = new FakeSender(NotificationChannel.Email);
        var slack = new FakeSender(NotificationChannel.Slack);

        Assert.Equal(0, await Service(fixture.Db, email, slack).ProcessPendingAsync());

        Assert.Empty(email.Messages);
        Assert.Empty(slack.Messages);
        Assert.Equal("Previous failure", rows[1].LastError);
        Assert.All(rows, row => Assert.Equal(1, row.AttemptCount));
    }

    private static DeliveryProcessingService Service(AppDbContext db, params INotificationSender[] senders) =>
        new(db, senders, new FixedTimeProvider(), NullLogger<DeliveryProcessingService>.Instance);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(AttemptTime);
    }

    private sealed class FakeSender(
        NotificationChannel channel,
        Func<NotificationMessage, Task>? onSend = null) : INotificationSender
    {
        public NotificationChannel Channel => channel;
        public List<NotificationMessage> Messages { get; } = [];
        public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            if (onSend is not null)
            {
                await onSend(message);
            }
        }
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
        public async Task SeedAsync(params NotificationChannel[] channels)
        {
            var alert = new Alert
            {
                Name = "Fixture alert",
                MinimumMagnitude = 4.5,
                IsEnabled = true,
                CreatedAtUtc = AttemptTime.AddDays(-1),
                UpdatedAtUtc = AttemptTime.AddDays(-1)
            };
            var earthquake = new EarthquakeEvent
            {
                SourceEventId = "fixture",
                OccurredAtUtc = AttemptTime.AddMinutes(-5),
                FirstSeenAtUtc = AttemptTime.AddMinutes(-4),
                Magnitude = 5.0,
                Place = "Fixture location",
                Title = "Fixture earthquake",
                SourceUrl = "https://example.test/fixture"
            };
            Db.Alerts.Add(alert);
            Db.EarthquakeEvents.Add(earthquake);
            await Db.SaveChangesAsync();
            Db.Deliveries.AddRange(channels.Select(channel => new Delivery
            {
                EarthquakeEventId = earthquake.Id,
                AlertId = alert.Id,
                Channel = channel,
                Status = DeliveryStatus.Pending
            }));
            await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
