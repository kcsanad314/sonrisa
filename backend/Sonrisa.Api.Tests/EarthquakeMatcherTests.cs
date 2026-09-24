using Sonrisa.Api.Alerts;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Tests;

public sealed class EarthquakeMatcherTests
{
    private static readonly DateTime AlertCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly EarthquakeMatcher _matcher = new();

    [Theory]
    [InlineData(4.9, false)]
    [InlineData(5.0, true)]
    [InlineData(5.1, true)]
    public void Matches_UsesInclusiveMagnitudeThreshold(double magnitude, bool expected)
    {
        var earthquake = new EarthquakeEvent
        {
            Magnitude = magnitude,
            OccurredAtUtc = AlertCreatedAt.AddMinutes(1)
        };
        var alert = NewAlert();

        Assert.Equal(expected, _matcher.Matches(earthquake, alert));
    }

    [Fact]
    public void Matches_RejectsDisabledAlert()
    {
        var earthquake = new EarthquakeEvent
        {
            Magnitude = 6.0,
            OccurredAtUtc = AlertCreatedAt.AddMinutes(1)
        };
        var alert = NewAlert();
        alert.IsEnabled = false;

        Assert.False(_matcher.Matches(earthquake, alert));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Matches_OnlyAcceptsEventsAfterAlertCreation(int minutesAfterCreation, bool expected)
    {
        var earthquake = new EarthquakeEvent
        {
            Magnitude = 6.0,
            OccurredAtUtc = AlertCreatedAt.AddMinutes(minutesAfterCreation)
        };

        Assert.Equal(expected, _matcher.Matches(earthquake, NewAlert()));
    }

    private static Alert NewAlert() => new()
    {
        MinimumMagnitude = 5.0,
        IsEnabled = true,
        CreatedAtUtc = AlertCreatedAt
    };
}
