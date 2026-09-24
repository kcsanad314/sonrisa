# Sonrisa operator UI

Run the .NET API from `backend/Sonrisa.Api` with `dotnet run`, then run `npm ci` and `npm start` here. Open `http://localhost:4200`. The Angular dev server proxies `/api` to the API at `http://localhost:5003`.

Use the page to manage alerts, inspect recent earthquakes and delivery results, or create a Development-only demo earthquake. The demo uses the regular matching and delivery flow. Configure email and Slack destinations as described in the repository README before testing real sends.
