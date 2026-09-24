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
- Live smoke test: ran the API against the configured USGS M2.5+ past-day feed using a disposable SQLite database. The first poll fetched and parsed the feed, stored 29 earthquakes with 29 distinct USGS IDs, established the baseline at `2026-09-24 18:06:37 UTC`, and created 0 deliveries as expected. Stopped the app and removed the disposable database afterward. Notification sending was not tested in this milestone.
