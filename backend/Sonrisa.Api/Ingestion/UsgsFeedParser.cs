using System.Text.Json;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Ingestion;

public static class UsgsFeedParser
{
    public static IReadOnlyList<EarthquakeEvent> Parse(string json, ILogger logger)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
            type.GetString() != "FeatureCollection" ||
            !root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("USGS response is not a GeoJSON FeatureCollection.");
        }

        var events = new List<EarthquakeEvent>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features.EnumerateArray())
        {
            if (!TryParseFeature(feature, out var earthquake))
            {
                logger.LogWarning("Skipping malformed or non-earthquake USGS feature.");
                continue;
            }

            if (seenIds.Add(earthquake.SourceEventId))
            {
                events.Add(earthquake);
            }
        }

        return events;
    }

    private static bool TryParseFeature(JsonElement feature, out EarthquakeEvent earthquake)
    {
        earthquake = new EarthquakeEvent();
        if (feature.ValueKind != JsonValueKind.Object ||
            !feature.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(idElement.GetString()) ||
            !feature.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object ||
            !properties.TryGetProperty("type", out var eventType) || eventType.ValueKind != JsonValueKind.String ||
            eventType.GetString() != "earthquake" ||
            !properties.TryGetProperty("mag", out var magnitudeElement) || magnitudeElement.ValueKind != JsonValueKind.Number ||
            !magnitudeElement.TryGetDouble(out var magnitude) || !double.IsFinite(magnitude) ||
            !properties.TryGetProperty("time", out var timeElement) || timeElement.ValueKind != JsonValueKind.Number ||
            !timeElement.TryGetInt64(out var epochMilliseconds) ||
            !properties.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        DateTime occurredAtUtc;
        try
        {
            occurredAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var id = idElement.GetString()!;
        earthquake = new EarthquakeEvent
        {
            SourceEventId = id,
            OccurredAtUtc = occurredAtUtc,
            Magnitude = magnitude,
            Place = OptionalString(properties, "place") ?? "Unknown location",
            Title = OptionalString(properties, "title") ?? $"Earthquake {id}",
            SourceUrl = url.ToString()
        };
        return true;
    }

    private static string? OptionalString(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
}
