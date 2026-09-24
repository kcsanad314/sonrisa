namespace Sonrisa.Api.Data.Entities;

public enum DeliveryChannel
{
    Email,
    Slack
}

public enum DeliveryStatus
{
    Pending,
    Sent,
    Failed
}

public sealed class Delivery
{
    public int Id { get; set; }
    public int EarthquakeEventId { get; set; }
    public int AlertId { get; set; }
    public DeliveryChannel Channel { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
}
