namespace Sonrisa.Api.Data.Entities;

public sealed class EarthquakeEvent
{
    public int Id { get; set; }
    public string SourceEventId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public double Magnitude { get; set; }
    public string Place { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}
