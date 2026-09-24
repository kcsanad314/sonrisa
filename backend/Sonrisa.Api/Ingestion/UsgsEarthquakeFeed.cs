using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Ingestion;

public sealed class UsgsEarthquakeFeed(HttpClient client, ILogger<UsgsEarthquakeFeed> logger) : IEarthquakeFeed
{
    public const string FeedUrl = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson";

    public async Task<IReadOnlyList<EarthquakeEvent>> FetchAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(FeedUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return UsgsFeedParser.Parse(json, logger);
    }
}
