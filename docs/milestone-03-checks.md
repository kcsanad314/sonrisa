# Milestone 03: USGS ingestion

- Branch: `codex/milestone-03-usgs-ingestion` (created from `main`).
- Feed: USGS M2.5+ past-day GeoJSON, polled sequentially every five minutes.
- First valid response establishes one persisted baseline and stores events without deliveries.
- Subsequent polls insert only unseen USGS IDs; only newly inserted events occurring after the baseline enter matching. Existing IDs are not updated.
- A malformed feature is skipped; an invalid whole feed or failed fetch does not initialize the baseline. New event insertion and pending delivery creation share a SQLite transaction.
- `dotnet test Sonrisa.slnx --no-restore`: 26 passed, 0 failed.
- `dotnet build Sonrisa.slnx --no-restore`: passed with no warnings or errors.
- Applied both migrations to a fresh SQLite file and confirmed `IngestionStates` exists without a baseline row. Removed the check database afterward.
- `dotnet-ef migrations has-pending-model-changes`: no changes.
- No USGS live fetch or notification sending is required for these deterministic tests.
