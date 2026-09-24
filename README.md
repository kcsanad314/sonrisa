# Sonrisa

Local, single-user earthquake alert prototype. The repository contains the .NET 10/Angular 22 scaffold, EF Core 10 SQLite model, one initial migration, and fixture-tested earthquake matching and pending-delivery creation. Polling and notification sending are later milestones.

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
dotnet tool run dotnet-ef database update
dotnet run
```

The API currently exposes `GET /health`. The SQLite file is created in `backend/Sonrisa.Api/sonrisa.db` when the migration is applied from that directory. It is ignored by Git.

In another terminal:

```powershell
cd frontend
npm ci
npm run build
npm start
```

See [the implementation plan](docs/implementation-plan.md) and [decisions](docs/decisions.md) for scope and next steps.
