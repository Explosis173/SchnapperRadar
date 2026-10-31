namespace SchnapperRadar.Domain.ValueObjects;

public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    private const double EarthRadiusKm = 6371.0;

    public double DistanceKmTo(GeoPoint other)
    {
        var dLat = ToRad(other.Latitude - Latitude);
        var dLon = ToRad(other.Longitude - Longitude);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRad(Latitude)) * Math.Cos(ToRad(other.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
