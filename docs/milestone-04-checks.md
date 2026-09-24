# Milestone 04: notification sending

- Branch: `codex/milestone-04-notifications` (created from local `main` after the approved ingestion merge).
- Pending deliveries are processed through `INotificationSender`; Email uses SMTP and Slack uses an incoming webhook. The matcher and Alert model are unchanged.
- Sender destinations and credentials are read from local configuration or environment variables. They are not stored in the database or committed.
- Attempt count and UTC time are saved before sending. Each delivery becomes Sent or Failed; a sender failure is recorded without stopping the next pending delivery. Failed rows are not retried automatically.
- SQLite-backed tests use fake senders. Slack HTTP tests use an in-memory handler; the automated suite does not contact Slack or SMTP.
- `dotnet build Sonrisa.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Sonrisa.slnx --no-restore --no-build`: 34 passed, 0 failed.
- No database model change or migration was needed for this milestone.
