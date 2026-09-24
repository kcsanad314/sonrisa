namespace Sonrisa.Api.Data.Entities;

// One row records the first successful feed snapshot. This is not poll history.
public sealed class IngestionState
{
    public int Id { get; set; } = 1;
    public DateTime BaselineAtUtc { get; set; }
}
