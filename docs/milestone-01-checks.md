# Scaffold and database checks

24 September 2026

- Git initially rejected the repository as `dubious ownership`: the repo is owned by the Windows user, while sandboxed commands run as a different account. Added only this repository to the user's global `safe.directory` list. Two milestone commits now work.
- Installed .NET SDK 10.0.401 in the user profile because only SDK 9 was initially available. Used Angular 22.2 with Node 24.19, a compatible pair.
- Restored EF Core SQLite and Design 10.0.12 and the repository-local `dotnet-ef` 10.0.12 tool.
- `dotnet build Sonrisa.slnx --no-restore`: passed with zero warnings and errors.
- `npm run build` in `frontend`: passed.
- Applied `InitialCreate` to the local SQLite file. Verified `Alerts`, `EarthquakeEvents`, and `Deliveries`, the migration history row, and the unique source-event and delivery indexes.
- `dotnet-ef migrations has-pending-model-changes`: none. A second `database update` applied nothing.

Initial builds failed inside the filesystem sandbox because MSBuild could not create a Windows temp directory and the Angular bundler could not traverse the user-profile path. Both builds passed when rerun with filesystem access, so these were environment failures rather than project build failures. No matching, polling, or notification code was added.
