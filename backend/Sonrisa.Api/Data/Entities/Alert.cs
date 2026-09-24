namespace Sonrisa.Api.Data.Entities;

public sealed class Alert
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double MinimumMagnitude { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
