using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Ingestion;

public interface IEarthquakeFeed
{
    Task<IReadOnlyList<EarthquakeEvent>> FetchAsync(CancellationToken cancellationToken);
}
