using System.Collections.Frozen;
using System.Globalization;
using SchnapperRadar.Application.Abstractions;
using SchnapperRadar.Domain.ValueObjects;

namespace SchnapperRadar.Infrastructure.Geo;

/// <summary>
/// PLZ → Mittelpunkt, komplett offline (keine externen Geo-APIs, keine Standortdaten nach außen).
/// Datensatz: WZBSocialScienceCenter/plz_geocoord (Apache-2.0).
/// </summary>
internal sealed class OfflineGeoService : IGeoService
{
    private const string ResourceName = "SchnapperRadar.plz_geocoord.csv";
    private static readonly Lazy<FrozenDictionary<string, GeoPoint>> Data = new(Load);

    public GeoPoint? Resolve(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return null;
        return Data.Value.TryGetValue(postalCode.Trim(), out var p) ? p : null;
    }

    private static FrozenDictionary<string, GeoPoint> Load()
    {
        using var stream = typeof(OfflineGeoService).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Ressource {ResourceName} fehlt.");
        using var reader = new StreamReader(stream);
        var map = new Dictionary<string, GeoPoint>();
        reader.ReadLine(); // Header
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                map[parts[0].Trim()] = new GeoPoint(lat, lon);
        }
        return map.ToFrozenDictionary();
    }
}
