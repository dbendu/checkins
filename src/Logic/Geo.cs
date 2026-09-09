using Domain.Models;

namespace Logic;

public static class Geo
{
    // Средний радиус Земли. Гаверсинус считает по сфере, а не по эллипсоиду:
    // на сотнях метров, которыми мы меряем, разница меньше метра.
    private const double EarthRadiusMeters = 6_371_000d;

    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        var lat1 = double.DegreesToRadians(a.Lat);
        var lat2 = double.DegreesToRadians(b.Lat);
        var dLat = lat2 - lat1;
        var dLon = double.DegreesToRadians(b.Lon - a.Lon);

        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(h)));
    }
}
