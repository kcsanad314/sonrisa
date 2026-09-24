# Alert model correction checks

24 September 2026

Historical check record: the two migrations described below were later consolidated into one clean initial migration before feature work began.

- Removed alert deletion and its timestamp. Channel selection is now `AlertChannel(AlertId, Channel)`, with a composite primary key. `Alert` has no per-channel flags.
- Backend build passed with zero warnings and errors.
- Applied `SimplifyAlertsAndAddChannels` to the local SQLite database. Confirmed the old columns are absent, the new table has the composite key, and `PRAGMA foreign_key_check` reports no violations.
- EF reports no pending model changes; a second database update applied nothing.
- Applied both migrations to a throwaway SQLite database with old email-only, Slack-only, both, and neither selections. The migration preserved the four expected channel rows.
- Verified all six user prompts in `prompt-history.md` match the conversation text exactly and remain in chronological order.

EF warned that dropping SQLite columns requires rebuilding `Alerts` and briefly disabling foreign keys outside a transaction. This is expected for this local schema change. The populated throwaway database check and foreign key check passed.
