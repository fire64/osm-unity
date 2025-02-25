using System;
using UnityEngine;

public static class MercatorProjection
{
    private static readonly double R_MAJOR = 6378137.0;
    private static readonly double R_MINOR = 6356752.3142;
    private static readonly double RATIO = R_MINOR / R_MAJOR;
    private static readonly double ECCENT = Math.Sqrt(1.0 - (RATIO * RATIO));
    private static readonly double COM = 0.5 * ECCENT;

    private static readonly double DEG2RAD = Math.PI / 180.0;
    private static readonly double RAD2Deg = 180.0 / Math.PI;
    private static readonly double PI_2 = Math.PI / 2.0;

    public static double[] toPixel(double lon, double lat)
    {
        return new double[] { lonToX(lon), latToY(lat) };
    }

    public static double[] toGeoCoord(double x, double y)
    {
        return new double[] { xToLon(x), yToLat(y) };
    }

    public static double lonToX(double lon)
    {
        return R_MAJOR * DegToRad(lon);
    }

    public static double latToY(double lat)
    {
        lat = Math.Min(89.5, Math.Max(lat, -89.5));
        double phi = DegToRad(lat);
        double sinphi = Math.Sin(phi);
        double con = ECCENT * sinphi;
        con = Math.Pow(((1.0 - con) / (1.0 + con)), COM);
        double ts = Math.Tan(0.5 * ((Math.PI * 0.5) - phi)) / con;
        return 0 - R_MAJOR * Math.Log(ts);
    }

    public static double xToLon(double x)
    {
        return RadToDeg(x) / R_MAJOR;
    }

    public static double yToLat(double y)
    {
        double ts = Math.Exp(-y / R_MAJOR);
        double phi = PI_2 - 2 * Math.Atan(ts);
        double dphi = 1.0;
        int i = 0;
        while ((Math.Abs(dphi) > 0.000000001) && (i < 15))
        {
            double con = ECCENT * Math.Sin(phi);
            dphi = PI_2 - 2 * Math.Atan(ts * Math.Pow((1.0 - con) / (1.0 + con), COM)) - phi;
            phi += dphi;
            i++;
        }
        return RadToDeg(phi);
    }

    private static double RadToDeg(double rad)
    {
        return rad * RAD2Deg;
    }

    private static double DegToRad(double deg)
    {
        return deg * DEG2RAD;
    }

    public static double lonToTileX(double lon, int z)
    {
        return (lon + 180.0) / 360.0 * Math.Pow(2.0, z);
    }

    public static double latToTileY(double lat, int z)
    {
        return (1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) +
                               1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2.0, z);
    }

    public static double tileXToLon(double x, int z)
    {
        return x / Math.Pow(2.0, z) * 360.0 - 180.0;
    }

    public static double tileYToLat(double y, int z)
    {
        double n = Math.PI - 2.0 * Math.PI * y / Math.Pow(2.0, z);
        return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }

    public static double[] GetBoundingBox(double lon, double lat, float radius)
    {
        const double metersPerDegree = 111111.0;

        // Рассчитываем смещение по широте
        double deltaLat = radius / metersPerDegree;

        // Рассчитываем смещение по долготе
        double deltaLon;
        if (Math.Abs(lat) >= 90.0)
        {
            deltaLon = 180.0; // На полюсах долгота не имеет значения
        }
        else
        {
            double latRad = lat * Math.PI / 180.0;
            double cosLat = Math.Cos(latRad);
            deltaLon = Math.Abs(cosLat) > 1e-9
                ? radius / (metersPerDegree * cosLat)
                : 180.0; // Избегаем деления на ноль
        }

        // Вычисляем границы
        double minLon = NormalizeLongitude(lon - deltaLon);
        double maxLon = NormalizeLongitude(lon + deltaLon);
        double minLat = Math.Max(lat - deltaLat, -90.0);
        double maxLat = Math.Min(lat + deltaLat, 90.0);

        return new[] { minLon, minLat, maxLon, maxLat };
    }

    private static double NormalizeLongitude(double longitude)
    {
        // Приводим долготу к диапазону [-180, 180)
        longitude %= 360;
        if (longitude < -180)
            longitude += 360;
        else if (longitude >= 180)
            longitude -= 360;
        return longitude;
    }

    // Основная функция для получения размера тайла
    public static Vector2 GetTileSizeInMeters(int x, int y, int zoom)
    {
        // Получаем угловые координаты тайла
        double[] topLeft = GetTileLatLon(x, y, zoom);
        double[] bottomRight = GetTileLatLon(x + 1, y + 1, zoom);

        // Конвертируем в метры
        Vector2 topLeftMeters = LatLonToMeters(topLeft[0], topLeft[1]);
        Vector2 bottomRightMeters = LatLonToMeters(bottomRight[0], bottomRight[1]);

        // Рассчитываем размеры
        float width = Mathf.Abs(bottomRightMeters.x - topLeftMeters.x);
        float height = Mathf.Abs(topLeftMeters.y - bottomRightMeters.y);

        return new Vector2(width, height);
    }

    // Перевод координат тайла в географические координаты
    public static double[] GetTileLatLon(int x, int y, int zoom)
    {
        double n = Math.Pow(2, zoom);
        double lon = x / n * 360.0 - 180.0;

        double arg = Math.PI * (1 - 2.0 * y / n);
        double latRad = Math.Atan(Math.Sinh(arg)); // Исправленная строка

        double lat = latRad * 180.0 / Math.PI;
        return new double[] { lat, lon };
    }

    // Перевод географических координат в метры по проекции Меркатора
    public static Vector2 LatLonToMeters(double lat, double lon)
    {
        // Earth radius in meters
        const double R = 6378137;

        double latRad = lat * Math.PI / 180.0;
        double lonRad = lon * Math.PI / 180.0;

        double x = R * lonRad;
        double y = R * Math.Log(Math.Tan((Math.PI / 4) + (latRad / 2)));

        return new Vector2((float)x, (float)y);
    }

}