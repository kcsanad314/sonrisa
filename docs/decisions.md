# Prototype decisions

This is a local, single-user exercise prototype, not a public alerting service.

- **Scope:** Earthquakes are the only event type. Alerts use a minimum magnitude and select email, Slack, or both. News, markets, and generic rules are out of scope.
- **Stack:** ASP.NET Core on .NET 10, Angular, EF Core 10, and SQLite. ASP.NET Core, Angular, and EF Core are familiar to the developer. SQLite is new to the developer but keeps local setup simple and needs no database server. Use a compatible Angular/Node pair when scaffolding.
- **Model:** Use `EarthquakeEvent`, not a generic event entity or type discriminator. Keep USGS translation in a source adapter, earthquake rules in a matcher, and delivery behind channel-specific senders. Do not build a plugin system.
- **Alert channels:** Store selected channels in `AlertChannel` rows with a unique alert/channel pair. Adding a channel requires a sender and enum value, not a new `Alert` column. Alerts can be created, edited, enabled, and disabled; deletion is outside this prototype.
- **Access:** No accounts or authentication. The app is run locally by one operator. The admin page is an operational view, not a separate role. Do not deploy it publicly as-is.
- **Destinations:** Configure the email recipient and Slack webhook locally; alerts only select enabled channels. Keep secrets out of Git.
- **Delivery:** Persist one delivery per earthquake, alert, and channel. A pending-only worker dispatches through channel senders, records attempts and failures, and does not automatically retry Failed rows. Do not promise exactly-once delivery; a provider timeout or process crash may leave the send outcome uncertain.
- **Source behavior:** Poll the USGS M2.5+ past-day feed every five minutes. The first successful poll stores a single baseline timestamp and existing events without deliveries. Later polls consider only newly inserted events whose occurrence time is after that baseline; matching also requires occurrence after alert creation. Treat updates to an existing source event as the same event and do not send update alerts in this version. An outage longer than the feed window can miss events.

Adding an event type later may justify a broader event model. It is not a reason to add one now.
