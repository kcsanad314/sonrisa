# Sonrisa

Local, single-user earthquake alert prototype built with .NET 10, Angular 22, EF Core 10, and SQLite. The operator page manages alerts and shows recent earthquakes and delivery results. The backend polls USGS and sends pending notifications through email or Slack.

## Prerequisites

- .NET 10 SDK
- Node.js supported by Angular 22 (for example, Node 24.15 or newer) and npm

If .NET 10 was installed under your user profile while an older `dotnet` is first on `PATH`, prepend it for the current PowerShell session: `$env:PATH="$env:USERPROFILE\.dotnet;$env:PATH"`.

## Build and initialize

From the repository root:

```powershell
dotnet tool restore
dotnet restore Sonrisa.slnx
dotnet build Sonrisa.slnx
dotnet test Sonrisa.slnx
cd backend/Sonrisa.Api
dotnet run
```

The API currently exposes `GET /health`. Startup applies the EF migrations and creates the ignored `backend/Sonrisa.Api/sonrisa.db` file when run from that directory. The background worker immediately reads the [USGS M2.5+ past-day feed](https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson), then polls every five minutes. The first successful poll stores events and a single baseline timestamp without creating deliveries. Later polls create pending deliveries only for newly inserted events whose occurrence time is after that timestamp. Existing USGS IDs are ignored even if USGS revises their data, so a magnitude revision across an alert threshold will not generate a delivery. Outages longer than the feed's one-day window can miss events.

## Notification configuration

Set these environment variables locally before starting the API. Use your actual values; do not commit credentials or webhook URLs. ASP.NET Core maps `__` to nested configuration keys.

| Channel | Environment variables |
| --- | --- |
| Email | `Notifications__Email__SmtpHost`, `Notifications__Email__Port` (default `587`), `Notifications__Email__UseSsl` (default `true`, STARTTLS), `Notifications__Email__From`, `Notifications__Email__To`, and optionally `Notifications__Email__Username` and `Notifications__Email__Password` together. |
| Slack | `Notifications__Slack__WebhookUrl` (an HTTPS incoming webhook URL). |

The delivery worker checks Pending rows every 30 seconds, processes at most 100 per pass, and records the attempt time and count before sending. It marks each result Sent or Failed and records a useful error without storing destinations or credentials. Other pending deliveries continue after a send failure. Failed rows are not retried automatically. A crash after a provider accepts a message but before Sent is saved can result in a duplicate send on restart.

## Local operator demo

Run the API with its Development launch profile (`dotnet run` from `backend/Sonrisa.Api`) and the Angular dev server (`npm start` from `frontend`). Open `http://localhost:4200`. The Angular dev server proxies `/api` to the API on `localhost:5003`.

Create an enabled alert, choose Email, Slack, or both, and set a threshold at or below the demo magnitude. Use **Create demo earthquake** in the Local demo section. This Development-only action writes one marked fixture event and calls the same matching and pending-delivery creation service as USGS ingestion. The notification worker processes those Pending rows on its next pass; the page refreshes results every 15 seconds. Without sender credentials, attempts become Failed with a configuration error. Configure controlled destinations before using the demo if you want to verify real sending.

In another terminal:

```powershell
cd frontend
npm ci
npm run build
npm start
```

See [the implementation plan](docs/implementation-plan.md) and [decisions](docs/decisions.md) for scope and next steps.
