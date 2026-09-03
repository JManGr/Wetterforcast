using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace WeatherApp.Services;

/// <summary>
/// DWD Niederschlagsradar via WMS (GeoServer).
/// Layer: dwd:Niederschlagsradar – 1km, 5min, mm/h, Analyse+Vorhersage
/// GetCapabilities: https://maps.dwd.de/geoserver/wms?SERVICE=WMS&REQUEST=GetCapabilities
/// GetMap: https://maps.dwd.de/geoserver/ows?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=dwd:Niederschlagsradar&CRS=EPSG:4326&BBOX=...
/// </summary>
public class DwdRadarService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private static readonly HttpClient _shared = CreateShared();

    private static HttpClient CreateShared()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherApp/1.0 (https://github.com/Wetterforcast)");
        return c;
    }

    public DwdRadarService() : this(_shared, false) { }
    public DwdRadarService(HttpClient http, bool ownsClient = true)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsClient = ownsClient;
    }

    private static List<DateTime>? _cachedTimes;
    private static DateTime _cacheUntil;
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Liefert verfügbare Zeiten für Niederschlagsradar (Dimension time).
    /// Parsed GetCapabilities: <Dimension name="time">start/end/PT5M</Dimension>
    /// Bei Timeout/Fehler Fallback: generiert lokal 5-Min-Raster um Jetzt (ohne Netzwerk).
    /// </summary>
    public async Task<List<DateTime>> GetAvailableTimesAsync(CancellationToken ct = default)
    {
        // Cache 10 Minuten gültig – GetCapabilities ist langsam (~16s) und groß (800KB)
        // Lock + Kopie: verhindert Races bei parallelen Loads und Mutation von außen.
        lock (_cacheLock)
        {
            if (_cachedTimes != null && DateTime.UtcNow < _cacheUntil)
                return new List<DateTime>(_cachedTimes);
        }

        const string capUrl = "https://maps.dwd.de/geoserver/wms?SERVICE=WMS&REQUEST=GetCapabilities";
        try
        {
            // Eigenes Timeout für Capabilities (DWD langsam) – 25s
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(25));
            string xml = await _http.GetStringAsync(capUrl, cts.Token);

            var m = Regex.Match(xml, @"<Dimension\s+name=""time""[^>]*>([^<]+)</Dimension>", RegexOptions.Singleline);
            if (m.Success)
            {
                string val = m.Groups[1].Value.Trim();
                var parts = val.Split('/');
                if (parts.Length == 3 &&
                    DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var start) &&
                    DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var end))
                {
                    var interval = System.Xml.XmlConvert.ToTimeSpan(parts[2]);
                    var list = new List<DateTime>();
                    for (var t = start; t <= end; t = t.Add(interval))
                        list.Add(t);
                    lock (_cacheLock)
                    {
                        _cachedTimes = list;
                        _cacheUntil = DateTime.UtcNow.AddMinutes(10);
                    }
                    return new List<DateTime>(list);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Fallthrough zu lokalem Fallback – DWD langsam/timeout ist bekannt (GetCapabilities ~16s)
            System.Diagnostics.Debug.WriteLine($"DWD GetCapabilities Fallback: {ex.Message}");
        }

        // Fallback: generiere 5-Min-Raster lokal (2h Vergangenheit bis 30 Min Zukunft) – funktioniert auch offline
        var now = DateTime.UtcNow;
        var alignedNow = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute - now.Minute % 5, 0, DateTimeKind.Utc);
        var fallback = new List<DateTime>();
        for (int i = -24; i <= 6; i++) // -2h .. +30min
            fallback.Add(alignedNow.AddMinutes(i * 5));
        lock (_cacheLock)
        {
            _cachedTimes = fallback;
            _cacheUntil = DateTime.UtcNow.AddMinutes(5);
        }
        return new List<DateTime>(fallback);
    }

    // 150km Radius = 300km Durchmesser → Δlat 1.35° (111km/°) , Δlon ~2.15° bei 51°N (cos 51°≈0.629)
    // Alt 100km: 0.90° / 1.43° – behalten für Kompatibilität
    public const double DeltaLat100km = 0.90;
    public const double DeltaLon100km = 1.43;
    public const double DeltaLat150km = 1.35;
    public const double DeltaLon150km = 2.15;

    /// <summary>
    /// Liefert Δlon für gegebene Distanz bei Breite (korrigiert um cos(lat)).
    /// </summary>
    public static double GetDeltaLonForLat(double lat, double km = 100) => km / (111.32 * Math.Cos(lat * Math.PI / 180.0));
    public static double GetDeltaLatForKm(double km) => km / 111.0;

    /// <summary>
    /// Baut WMS GetMap URL für ein Radar-Bild zentriert auf lat/lon.
    /// BBOX: lat±DeltaLat, lon±DeltaLon, EPSG:4326 (Axis order lat,lon für WMS 1.3.0) – 100km Quadrat.
    /// Ursprung: Top-Left (0,0) = Nord-West (latMax, lonMin), Bottom-Right (512,512) = Süd-Ost (latMin, lonMax).
    /// Mitte (256,256) = lat/lon des Ortes. Pixel: X = (lon - lonMin)/(lonMax-lonMin)*512, Y = (latMax - lat)/(latMax-latMin)*512.
    /// </summary>
    public static string BuildMapUrl(double lat, double lon, DateTime timeUtc, int width = 512, int height = 512, double? deltaLat = null, double? deltaLon = null)
    {
        double dLat = deltaLat ?? DeltaLat100km;
        double dLon = deltaLon ?? DeltaLon100km;
        double latMin = lat - dLat;
        double latMax = lat + dLat;
        double lonMin = lon - dLon;
        double lonMax = lon + dLon;
        string bbox = string.Create(CultureInfo.InvariantCulture, $"{latMin:F5},{lonMin:F5},{latMax:F5},{lonMax:F5}");
        string timeStr = timeUtc.ToString("yyyy-MM-ddTHH:mm:ss.000Z", CultureInfo.InvariantCulture);
        return $"https://maps.dwd.de/geoserver/ows?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=dwd:Niederschlagsradar&STYLES=&CRS=EPSG:4326&BBOX={bbox}&WIDTH={width}&HEIGHT={height}&FORMAT=image/png&TRANSPARENT=true&TIME={Uri.EscapeDataString(timeStr)}";
    }

    /// <summary>
    /// Baut URL für aktuelles Radar (TIME=current)
    /// </summary>
    public static string BuildCurrentMapUrl(double lat, double lon, int width = 512, int height = 512, double? deltaLat = null, double? deltaLon = null)
    {
        double dLat = deltaLat ?? DeltaLat100km;
        double dLon = deltaLon ?? DeltaLon100km;
        double latMin = lat - dLat;
        double latMax = lat + dLat;
        double lonMin = lon - dLon;
        double lonMax = lon + dLon;
        string bbox = string.Create(CultureInfo.InvariantCulture, $"{latMin:F5},{lonMin:F5},{latMax:F5},{lonMax:F5}");
        return $"https://maps.dwd.de/geoserver/ows?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=dwd:Niederschlagsradar&STYLES=&CRS=EPSG:4326&BBOX={bbox}&WIDTH={width}&HEIGHT={height}&FORMAT=image/png&TRANSPARENT=true&TIME=current";
    }

    public async Task<byte[]?> GetMapBytesAsync(string url, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        // DWD liefert bei Fehler manchmal XML statt PNG – prüfen
        if (bytes.Length > 0 && bytes[0] == (byte)'<' ) return null;
        return bytes;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
