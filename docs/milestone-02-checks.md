# Matching and delivery creation checks

The branch adds only an earthquake matcher and a service that creates pending delivery rows for matching alerts and selected channels. It does not fetch USGS data or send email or Slack messages.

- `dotnet test Sonrisa.slnx --no-restore`: 15 passed, 0 failed.
- Matcher tests cover magnitude below/equal/above threshold, disabled alerts, and events before/at/after alert creation.
- SQLite-backed tests cover channel selection, no-channel behavior, overlapping alerts, pending defaults, reprocessing without resetting an existing delivery, and the unique event/alert/channel constraint.
- The delivery service reads an already-persisted event. It does not rescan historical events or dispatch messages; those boundaries belong to later milestones.
