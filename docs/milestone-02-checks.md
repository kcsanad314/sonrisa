# Matching and delivery creation checks

The branch adds only an earthquake matcher and a service that creates pending delivery rows for matching alerts and selected channels. It does not fetch USGS data or send email or Slack messages.

- `dotnet test Sonrisa.slnx --no-restore`: 17 passed, 0 failed.
- `dotnet build Sonrisa.slnx --no-restore`: passed with zero warnings and errors.
- Matcher tests cover magnitude below/equal/above threshold, disabled alerts, and events before/at/after alert creation.
- SQLite-backed tests cover channel selection, no-channel behavior, overlapping alerts, pending defaults, reprocessing without resetting an existing delivery, and the unique event/alert/channel constraint.
- Additional SQLite-backed tests confirm that a fresh `DbContext` does not duplicate or reset a failed delivery, and that two distinct earthquakes each create a delivery for the same alert. Both passed without an implementation change.
- The delivery service reads an already-persisted event. It does not rescan historical events or dispatch messages; those boundaries belong to later milestones.
