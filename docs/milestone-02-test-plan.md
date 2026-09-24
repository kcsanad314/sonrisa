# Proposed tests: matching and delivery creation

Review these before implementing the next milestone. Use fixture earthquakes and alerts; no USGS calls, email, or Slack sends.

| Case | Expected result |
| --- | --- |
| Magnitude below, equal to, and above the alert threshold | Below does not match; equal and above match. |
| Disabled alert | No match, regardless of magnitude. |
| Event occurred before or exactly when the alert was created | No match and no retrospective delivery; only later events qualify. |
| Enabled alert with Email only, Slack only, or both `AlertChannel` rows | Create exactly the selected pending delivery rows. |
| Enabled alert with no selected channel | Create no delivery; later UI validation should prevent saving this configuration. |
| Process the same event for the same alert twice | No second row for an event/alert/channel; an existing status and attempt count stay unchanged. |
| One event matches two enabled alerts | Each alert gets its own delivery rows. |
| New delivery defaults | Status is pending, attempt count is zero, and there is no last attempt or error. |

Use small unit tests for the threshold and enabled/time rules. Use SQLite-backed integration tests for delivery creation and uniqueness, because an in-memory collection would not verify the database constraint. Do not test source polling or actual senders in this milestone.
