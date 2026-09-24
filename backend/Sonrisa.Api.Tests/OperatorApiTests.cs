using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sonrisa.Api.Data;
using Sonrisa.Api.Data.Entities;
using Sonrisa.Api.Ingestion;
using Sonrisa.Api.Notifications;

namespace Sonrisa.Api.Tests;

public sealed class OperatorApiTests
{
    [Fact]
    public async Task ValidAlertPersistsWithSelectedChannels()
    {
        using var connection = OpenDatabase();
        using var factory = new ApiFactory(connection);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/alerts", new
        {
            name = "  Review alert  ", minimumMagnitude = 4.5,
            channels = new[] { "Email", "Slack" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alert = await db.Alerts.SingleAsync();
        Assert.Equal("Review alert", alert.Name);
        Assert.Equal(4.5, alert.MinimumMagnitude);
        Assert.True(alert.IsEnabled);
        Assert.Equal(new[] { NotificationChannel.Email, NotificationChannel.Slack },
            await db.AlertChannels.OrderBy(item => item.Channel)
                .Select(item => item.Channel).ToArrayAsync());
    }

    [Theory]
    [InlineData("{\"name\":\" \u0020\",\"minimumMagnitude\":4.5,\"channels\":[\"Email\"]}")]
    [InlineData("{\"name\":\"Too low\",\"minimumMagnitude\":2.0,\"channels\":[\"Email\"]}")]
    [InlineData("{\"name\":\"No channels\",\"minimumMagnitude\":4.5,\"channels\":[]}")]
    public async Task InvalidAlertIsRejectedWithoutPersistence(string json)
    {
        using var connection = OpenDatabase();
        using var factory = new ApiFactory(connection);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/alerts",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Alerts.ToListAsync());
        Assert.Empty(await db.AlertChannels.ToListAsync());
    }

    [Fact]
    public async Task DevelopmentDemoCreatesEventAndMatchingPendingDeliveries()
    {
        using var connection = OpenDatabase();
        using var factory = new ApiFactory(connection);
        using var client = factory.CreateClient();
        var alertResponse = await client.PostAsJsonAsync("/api/alerts", new
        {
            name = "M4.5+", minimumMagnitude = 4.5,
            channels = new[] { "Email", "Slack" }
        });
        alertResponse.EnsureSuccessStatusCode();

        var demoResponse = await client.PostAsJsonAsync("/api/demo/earthquake", new { magnitude = 5.0 });

        Assert.Equal(HttpStatusCode.OK, demoResponse.StatusCode);
        var result = await demoResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("deliveryCount").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var earthquake = await db.EarthquakeEvents.SingleAsync();
        Assert.StartsWith("demo-", earthquake.SourceEventId);
        Assert.Equal(5.0, earthquake.Magnitude);
        var deliveries = await db.Deliveries.OrderBy(item => item.Channel).ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.Equal(new[] { NotificationChannel.Email, NotificationChannel.Slack },
            deliveries.Select(item => item.Channel));
        Assert.All(deliveries, delivery =>
        {
            Assert.Equal(earthquake.Id, delivery.EarthquakeEventId);
            Assert.Equal(DeliveryStatus.Pending, delivery.Status);
            Assert.Equal(0, delivery.AttemptCount);
        });
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private sealed class ApiFactory(SqliteConnection connection) : WebApplicationFactory<AppDbContext>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                foreach (var worker in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                    (item.ImplementationType == typeof(EarthquakePollingWorker) ||
                     item.ImplementationType == typeof(DeliveryProcessingWorker))).ToArray())
                {
                    services.Remove(worker);
                }

                foreach (var options in services.Where(item =>
                    item.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToArray())
                {
                    services.Remove(options);
                }
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            });
        }
    }
}
