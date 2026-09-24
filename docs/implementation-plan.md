# Earthquake alert prototype: implementation plan

## Finished version

The local operator can create, edit, enable, and disable magnitude-threshold alerts; select email and/or Slack; and see recent earthquakes, alerts, and delivery results in an admin view. A scheduled poll ingests a documented USGS feed. A fixture-driven demo proves the full path without waiting for a live event. Setup instructions cover credentials and reproduction.

The prototype has no authentication, geographic rules, multiple event types, generic rule editor, Slack OAuth, digesting, or production reliability claim.

## System and data

One ASP.NET Core .NET 10 application hosts the API and polling job. Angular provides alert setup and the admin view. EF Core 10 stores data in a local SQLite file, avoiding a separate database server. The flow is:

`USGS adapter -> EarthquakeEvent -> magnitude matcher -> Delivery -> email/Slack sender -> delivery result`

| Record | Minimum fields and rule |
| --- | --- |
| `EarthquakeEvent` | ID, USGS event ID (unique), occurred UTC, first seen UTC, magnitude (`double`), place, title, source URL. |
| `Alert` | ID, name, minimum magnitude (`double`), enabled, created UTC, updated UTC. No deletion action. |
| `AlertChannel` | Alert ID and channel, unique together. Stores an alert's selected channels without per-channel columns on `Alert`. |
| `Delivery` | ID, earthquake ID, alert ID, channel, status, attempt count, last attempt UTC, last error. Unique on earthquake + alert + channel. |
| `IngestionState` | One fixed-ID row containing the first successful feed baseline time. No poll history. |

Store timestamps as UTC `DateTime`. No generic event payload, source registry, user table, destination table, or poll-history table is needed. The baseline row prevents first-startup backlog notifications. Logs can show poll failures; the admin page focuses on events and delivery results. Failed deliveries remain visible and are not retried automatically.

Existing feed events must not trigger alerts created later. Repeated polls must not create repeated earthquakes or deliveries. Two overlapping alerts may each produce a notification; document this behavior.

## Build and verify

| Time | Build / proposed commit | Check before continuing |
| --- | --- | --- |
| 0–2 h | Scope and decisions. `docs: define prototype scope` | Acceptance criteria and exclusions are clear; start a prompt and decision log. |
| 2–5 h | ASP.NET Core, Angular, EF Core/SQLite, initial migration. `build: establish app and data model` | Clean checkout starts; migration applies; secrets and database file are ignored. |
| 5–9 h | Fixture event, matching, delivery records, test sender. `feat: match earthquakes and track deliveries` | Threshold boundary works; repeat processing is deduplicated; prior events do not alert. |
| 9–12 h | USGS adapter and polling job. `feat: ingest earthquake feed` | Repeated poll is idempotent; source failure is logged; first poll sends no old events. |
| 12–16 h | Email and Slack senders. `feat: send email and Slack alerts` | Both channels send to controlled destinations; failures are recorded and visible. |
| 16–20 h | Alert setup and admin view. `feat: manage alerts and inspect results` | Create/edit/enable/disable work; recent events and failed deliveries are visible; fixture demo is repeatable. |
| 20–24 h | End-to-end review and submission artifacts. `docs: record demo and validation` | Fresh-checkout setup works; README, exact prompt history, decisions, checks, rejected outputs, and limits are in the repo. |

Prefer a small, working vertical slice over a generic rule engine, plugin loader, message broker, separate worker service, or elaborate admin dashboard. If setup or integrations consume extra time, reduce UI polish before cutting the end-to-end path or validation evidence.
