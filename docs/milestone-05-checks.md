# Milestone 05: operator API and UI

- Branch: `codex/milestone-05-ui-api` (created after the approved notification milestone was fast-forwarded into local `main`).
- API lists alerts, recent earthquakes, and recent delivery results. It creates and edits alerts, updates enabled state, and stores selected Email/Slack channels through the existing relation.
- Angular has one local operator page with those actions and views. Styling is intentionally minimal.
- The Development-only demo endpoint inserts one labeled fixture earthquake and calls the existing matching/delivery creation service in one transaction. The normal notification worker processes Pending rows.
- Disposable SQLite HTTP smoke test: `GET /health`, alert creation, editing the threshold and channels, disabling/enabling, recent earthquake and delivery reads, and the demo action all succeeded. Invalid magnitude and empty channel selection returned HTTP 400.
- The demo M5.0 event created two Pending deliveries for an alert selecting both channels. The normal worker attempted each once; with no credentials in the disposable run, both became Failed with a configuration error. The test database was removed afterward.
- Angular dev server returned the UI at `http://127.0.0.1:4200/` and proxied `/api/alerts` to the API.
- API tests now cover persisted alert/channel selection, rejected invalid configurations, and the Development demo creating Pending deliveries through the real matching flow. They use an in-memory SQLite database and disable background workers and external sends.
- Real notification test reported by the operator: an enabled Email + Slack alert matched a demo M5.0 earthquake; the email and Slack messages both arrived, and both delivery rows showed Sent with one attempt and no error. Credentials and the webhook URL were stored locally with .NET user-secrets and were not committed.
- Initial milestone check: `dotnet build Sonrisa.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet test Sonrisa.slnx --no-restore --no-build` passed 34 tests. Final checks after the API test addition are recorded below.
- Angular production build passed; `npm test -- --watch=false`: 3 passed. The test run used the bundled supported Node.js version.
- Final checks: backend Release build passed with 0 warnings and 0 errors; full backend Release suite passed 39 tests. Angular production build and all 3 UI tests passed again. Release output was used because the operator's live Debug API process was still running.
