# Sonrisa

Local, single-user earthquake alert prototype. The repository contains the .NET 10/Angular 22 scaffold, EF Core 10 SQLite model, fixture-tested earthquake matching, pending-delivery creation, and USGS ingestion. Notification sending is a later milestone.

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

The API currently exposes `GET /health`. Startup applies the EF migrations and creates the ignored `backend/Sonrisa.Api/sonrisa.db` file when run from that directory. The background worker immediately reads the [USGS M2.5+ past-day feed](https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson), then polls every five minutes. The first successful poll stores events and a single baseline timestamp without creating deliveries. Later polls create pending deliveries only for newly inserted events whose occurrence time is after that timestamp. Existing USGS IDs are ignored even if USGS revises their data, so a magnitude revision across an alert threshold will not generate a delivery. Outages longer than the feed's one-day window can miss events. Pending deliveries are not sent yet.

In another terminal:

```powershell
cd frontend
npm ci
npm run build
npm start
```

See [the implementation plan](docs/implementation-plan.md) and [decisions](docs/decisions.md) for scope and next steps.
