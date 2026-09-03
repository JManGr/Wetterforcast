using System;
using System.Collections.Generic;
using System.Linq;
using WeatherApp.Models;

namespace WeatherApp.Services;

/// <summary>
/// Baut Orientierungs-Pins im 150km-Umkreis (statische Fallback-Liste,
/// zweistufig: &gt;100k primär, 30–100k sekundär, nach Abstand).
/// Reine Logik ohne UI-State – ausgelagert aus dem ehemaligen God-ViewModel.
/// </summary>
public static class OrientationService
{
    private static readonly (string name, double lat, double lon, string place)[] _fallbackOver100k =
    [
        ("Berlin", 52.5200, 13.4050, "city"), ("Hamburg", 53.5511, 9.9937, "city"), ("München", 48.1351, 11.5820, "city"),
        ("Köln", 50.9375, 6.9603, "city"), ("Frankfurt", 50.1109, 8.6821, "city"), ("Stuttgart", 48.7758, 9.1829, "city"),
        ("Düsseldorf", 51.2277, 6.7735, "city"), ("Leipzig", 51.3397, 12.3731, "city"), ("Dortmund", 51.5137, 7.4653, "city"),
        ("Essen", 51.4556, 7.0116, "city"), ("Bremen", 53.0793, 8.8017, "city"), ("Dresden", 51.0504, 13.7373, "city"),
        ("Hannover", 52.3759, 9.7320, "city"), ("Nürnberg", 49.4521, 11.0767, "city"), ("Duisburg", 51.4344, 6.7623, "city"),
        ("Bochum", 51.4818, 7.2162, "city"), ("Wuppertal", 51.2562, 7.1508, "city"), ("Bielefeld", 52.0302, 8.5325, "city"),
        ("Bonn", 50.7374, 7.0982, "city"), ("Münster", 51.9607, 7.6261, "city"), ("Karlsruhe", 49.0069, 8.4037, "city"),
        ("Mannheim", 49.4875, 8.4660, "city"), ("Augsburg", 48.3705, 10.8978, "city"), ("Wiesbaden", 50.0782, 8.2398, "city"),
        ("Mönchengladbach", 51.1805, 6.4428, "city"), ("Gelsenkirchen", 51.5177, 7.0857, "city"), ("Braunschweig", 52.2689, 10.5268, "city"),
        ("Kiel", 54.3233, 10.1228, "city"), ("Chemnitz", 50.8278, 12.9214, "city"), ("Aachen", 50.7753, 6.0839, "city"),
        ("Halle", 51.4966, 11.9688, "city"), ("Magdeburg", 52.1205, 11.6276, "city"), ("Freiburg", 47.9990, 7.8421, "city"),
        ("Krefeld", 51.3388, 6.5853, "city"), ("Mainz", 50.0027, 8.2700, "city"), ("Lübeck", 53.8655, 10.6866, "city"),
        ("Erfurt", 50.9848, 11.0299, "city"), ("Oberhausen", 51.4696, 6.8513, "city"), ("Rostock", 54.0887, 12.1407, "city"),
        ("Kassel", 51.3127, 9.4797, "city"), ("Hagen", 51.3671, 7.4633, "city"), ("Hamm", 51.6739, 7.8159, "city"),
        ("Saarbrücken", 49.2402, 6.9969, "city"), ("Potsdam", 52.3906, 13.0645, "city"), ("Mülheim", 51.4269, 6.8857, "city"),
        ("Ludwigshafen", 49.4774, 8.4452, "city"), ("Leverkusen", 51.0354, 6.9860, "city"), ("Oldenburg", 53.1435, 8.2146, "city"),
        ("Osnabrück", 52.2799, 8.0472, "city"), ("Solingen", 51.1704, 7.0830, "city"), ("Heidelberg", 49.3988, 8.6724, "city"),
        ("Herne", 51.5369, 7.2009, "city"), ("Neuss", 51.1984, 6.6850, "city"), ("Darmstadt", 49.8728, 8.6512, "city"),
        ("Paderborn", 51.7189, 8.7575, "city"), ("Regensburg", 49.0134, 12.1016, "city"), ("Ingolstadt", 48.7665, 11.4258, "city"),
        ("Würzburg", 49.7913, 9.9534, "city"), ("Fürth", 49.4771, 10.9887, "city"), ("Wolfsburg", 52.4227, 10.7865, "city"),
        ("Ulm", 48.4011, 9.9876, "city"), ("Heilbronn", 49.1427, 9.2109, "city"), ("Pforzheim", 48.8973, 8.7050, "city"),
        ("Göttingen", 51.5413, 9.9158, "city"), ("Bottrop", 51.5238, 6.9280, "city"), ("Reutlingen", 48.4914, 9.2043, "city"),
        ("Koblenz", 50.3569, 7.5889, "city"), ("Bremerhaven", 53.5396, 8.5809, "city"), ("Recklinghausen", 51.6182, 7.1995, "city"),
        ("Bergisch Gladbach", 50.9896, 7.1248, "city"), ("Erlangen", 49.5897, 11.0110, "city"), ("Jena", 50.9271, 11.5864, "city"),
        ("Remscheid", 51.1809, 7.1988, "city"), ("Trier", 49.7499, 6.6371, "city"), ("Salzgitter", 52.1547, 10.3279, "city"),
        ("Moers", 51.4514, 6.6250, "city"), ("Siegen", 50.8747, 8.0243, "city"), ("Hildesheim", 52.1500, 9.9500, "city"),
        ("Cottbus", 51.7606, 14.3350, "city"), ("Kaiserslautern", 49.4440, 7.7690, "city"), ("Gütersloh", 51.9040, 8.3816, "city"),
    ];
    private static readonly (string name, double lat, double lon, string place)[] _fallback30to100k =
    [
        ("Offenbach", 50.0956, 8.7761, "city"), ("Schwerin", 53.6355, 11.4167, "city"), ("Witten", 51.4437, 7.3367, "city"),
        ("Gießen", 50.5867, 8.6806, "city"), ("Esslingen", 48.7406, 9.3088, "city"), ("Ludwigsburg", 48.8974, 9.1927, "city"),
        ("Düren", 50.8008, 6.4836, "city"), ("Ratingen", 51.2972, 6.8483, "city"), ("Tübingen", 48.5226, 9.0522, "city"),
        ("Flensburg", 54.7819, 9.4366, "city"), ("Villingen-Schwenningen", 48.0602, 8.4560, "city"), ("Gera", 50.8805, 12.0834, "city"),
        ("Hanau", 50.1329, 8.9173, "city"), ("Minden", 52.2891, 8.9147, "city"), ("Velbert", 51.3419, 7.0423, "city"),
        ("Marl", 51.6598, 7.0974, "city"), ("Lünen", 51.6167, 7.5228, "city"), ("Dorsten", 51.6600, 6.9640, "city"),
        ("Zwickau", 50.7272, 12.4896, "city"), ("Neumünster", 54.0723, 9.9841, "city"), ("Delmenhorst", 53.0515, 8.6318, "city"),
        ("Viersen", 51.2527, 6.3948, "city"), ("Norderstedt", 53.7067, 9.9902, "city"), ("Rheine", 52.2807, 7.4380, "city"),
        ("Weimar", 50.9796, 11.3235, "city"), ("Stralsund", 54.3091, 13.0812, "city"), ("Wetzlar", 50.5616, 8.5045, "city"),
        ("Neuwied", 50.4285, 7.4616, "city"), ("Gummersbach", 51.0269, 7.5673, "town"), ("Iserlohn", 51.3745, 7.6979, "town"),
        ("Marburg", 50.8090, 8.7707, "town"), ("Lippstadt", 51.6745, 8.3457, "town"), ("Winterberg", 51.1930, 8.5345, "town"),
        ("Arnsberg", 51.3967, 8.0643, "town"), ("Meschede", 51.3500, 8.2833, "town"), ("Soest", 51.5714, 8.1093, "town"),
        ("Brilon", 51.3945, 8.5671, "town"), ("Warburg", 51.4880, 9.1460, "town"), ("Korbach", 51.2788, 8.8726, "town"),
        ("Olpe", 51.0293, 7.8447, "town"), ("Lüdenscheid", 51.2235, 7.6313, "town"), ("Unna", 51.5340, 7.6889, "town"),
        ("Attendorn", 51.1268, 7.9028, "town"), ("Bad Berleburg", 51.0497, 8.4001, "town"),
    ];
    private static readonly (string name, double lat, double lon, string place)[] _fallbackCities = [.._fallbackOver100k, .._fallback30to100k];

    /// <summary>Großkreisdistanz in km (Haversine) – ersetzt OverpassService.</summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Baut Pins + Statuszeile für lat/lon (512×512-Canvas, breitenkorrigierte BBOX).
    /// </summary>
    public static (List<OrientationPin> Pins, string Status) BuildPins(double lat, double lon, string cityTitle)
    {
        double dLat = DwdRadarService.GetDeltaLatForKm(150);
        double dLon = DwdRadarService.GetDeltaLonForLat(lat, 150);
        double latMin = lat - dLat, latMax = lat + dLat;
        double lonMin = lon - dLon, lonMax = lon + dLon;

        var fallbackOver = _fallbackOver100k
            .Select(c => new { C = c, Dist = HaversineKm(lat, lon, c.lat, c.lon) })
            .Where(x => x.Dist <= 150 && x.Dist >= 5)
            .OrderBy(x => x.Dist)
            .ToList();
        var fallbackMid = _fallback30to100k
            .Select(c => new { C = c, Dist = HaversineKm(lat, lon, c.lat, c.lon) })
            .Where(x => x.Dist <= 150 && x.Dist >= 5)
            .OrderBy(x => x.Dist)
            .ToList();
        var fallbackTiered = new List<(string name, double lat, double lon, string place, double Dist)>();
        fallbackTiered.AddRange(fallbackOver.Select(x => (x.C.name, x.C.lat, x.C.lon, x.C.place, x.Dist)));
        if (fallbackTiered.Count < 15) fallbackTiered.AddRange(fallbackMid.Select(x => (x.C.name, x.C.lat, x.C.lon, x.C.place, x.Dist)).Take(15 - fallbackTiered.Count));
        if (fallbackTiered.Count == 0) fallbackTiered.AddRange(_fallbackCities.Select(c => new { C = c, Dist = HaversineKm(lat, lon, c.lat, c.lon) }).Where(x => x.Dist <= 150 && x.Dist >= 5).OrderBy(x => x.Dist).Take(15).Select(x => (x.C.name, x.C.lat, x.C.lon, x.C.place, x.Dist)));
        var pinsRaw = fallbackTiered.Take(15).Select(x =>
            {
                double xPix = (x.lon - lonMin) / (lonMax - lonMin) * 512;
                double yPix = (latMax - x.lat) / (latMax - latMin) * 512;
                return new OrientationPin
                {
                    Name = x.name, PlaceType = x.place, Lat = x.lat, Lon = x.lon,
                    DistanceKm = x.Dist, X = Math.Clamp(xPix, 12, 500), Y = Math.Clamp(yPix, 12, 500), ShowLabel = true
                };
            }).ToList();

        // Label-Überdeckung prüfen: nur anzeigen wenn keine/geringe Überdeckung (8px Toleranz)
        var pins = new List<OrientationPin>();
        var usedRects = new List<(double l, double t, double r, double b)>();
        const double labelH = 28;
        const double tol = 8; // geringe Überdeckung erlaubt
        foreach (var p in pinsRaw)
        {
            double w = Math.Clamp(14 + p.Name.Length * 6.5, 60, 95);
            double l = p.X + 8; // rechts neben Dot
            double t = p.Y - 12;
            double r = l + w;
            double b = t + labelH;
            // an Canvas-Rand anpassen (damit nicht außerhalb 512)
            if (r > 512) { l = p.X - w - 8; r = l + w; }
            if (t < 0) { t = p.Y + 8; b = t + labelH; }
            bool overlap = false;
            foreach (var u in usedRects)
            {
                double ix = Math.Min(r, u.r) - Math.Max(l, u.l);
                double iy = Math.Min(b, u.b) - Math.Max(t, u.t);
                if (ix > tol && iy > tol) { overlap = true; break; }
            }
            if (!overlap)
            {
                usedRects.Add((l, t, r, b));
                pins.Add(p); // ShowLabel true bleibt
            }
            else
            {
                // Dot behalten, Label ausblenden
                pins.Add(new OrientationPin { Name = p.Name, PlaceType = p.PlaceType, Lat = p.Lat, Lon = p.Lon, DistanceKm = p.DistanceKm, X = p.X, Y = p.Y, ShowLabel = false });
            }
        }
        int hidden = pinsRaw.Count - pins.Count(p => p.ShowLabel);
        string status = pins.Count > 0 ? $"{pins.Count(p => p.ShowLabel)}/{pins.Count} Labels sichtbar (150km Fallback) um {cityTitle}" + (hidden > 0 ? $" – {hidden} Labels wegen Überdeckung ausgeblendet" : "") : "Keine Orte 150km gefunden";

        return (pins, status);
    }
}
