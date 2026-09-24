using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Api;
using Sonrisa.Api.Data;
using Sonrisa.Api.Ingestion;
using Sonrisa.Api.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<EarthquakeMatcher>();
builder.Services.AddScoped<DeliveryCreationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IEarthquakeFeed, UsgsEarthquakeFeed>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<EarthquakeIngestionService>();
builder.Services.AddHostedService<EarthquakePollingWorker>();
builder.Services.AddTransient<INotificationSender, EmailNotificationSender>();
builder.Services.AddHttpClient<INotificationSender, SlackNotificationSender>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<DeliveryProcessingService>();
builder.Services.AddHostedService<DeliveryProcessingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapOperatorEndpoints();

app.Run();
