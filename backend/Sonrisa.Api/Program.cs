using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;
using Sonrisa.Api.Ingestion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<EarthquakeMatcher>();
builder.Services.AddScoped<DeliveryCreationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IEarthquakeFeed, UsgsEarthquakeFeed>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<EarthquakeIngestionService>();
builder.Services.AddHostedService<EarthquakePollingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
