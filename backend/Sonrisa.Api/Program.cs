using Microsoft.EntityFrameworkCore;
using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<EarthquakeMatcher>();
builder.Services.AddScoped<DeliveryCreationService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
