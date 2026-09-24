using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Alerts;

public sealed class EarthquakeMatcher
{
    public bool Matches(EarthquakeEvent earthquake, Alert alert) =>
        alert.IsEnabled
        && earthquake.OccurredAtUtc > alert.CreatedAtUtc
        && earthquake.Magnitude >= alert.MinimumMagnitude;
}
