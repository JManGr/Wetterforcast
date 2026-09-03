using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;

namespace WeatherApp.Services;

/// <summary>
/// Ruft Wetterdaten von der kostenlosen Met.no API ab.
/// https://api.met.no/weatherapi/locationforecast/2.0/compact
/// </summary>
public class WeatherService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    // AOT/Trim kompatibel via AppJsonContext

    // Gemeinsamer HttpClient – vermeidet Socket Exhaustion (met.no empfiehlt Wiederverwendung).
    private static readonly HttpClient _sharedClient = CreateSharedClient();

    private static HttpClient CreateSharedClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        // met.no verlangt aussagekräftigen User-Agent mit Kontakt.
        // WICHTIG: example.com wird von met.no mit 403 geblockt (getestet) – daher kein example.com verwenden!
        // Bei Veröffentlichung eigene Kontakt-URL/E-Mail eintragen, z. B. "WeatherApp/1.0 (https://github.com/deinname; contact@deinedomain.de)"
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherApp/1.0 (https://github.com/Wetterforcast)");
        return client;
    }

    public WeatherService() : this(_sharedClient, ownsClient: false) { }

    /// <summary>Für Tests injizierbar.</summary>
    public WeatherService(HttpClient httpClient, bool ownsClient = true)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Lädt die stündliche Vorhersage für einen geographischen Punkt.
    /// </summary>
    /// <param name="latitude">Breitengrad</param>
    /// <param name="longitude">Längengrad</param>
    /// <returns>Deserialisierte Met.no-Vorhersage</returns>
    public async Task<MetForecastResponse?> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        string url = "https://api.met.no/weatherapi/locationforecast/2.0/compact"
                   + $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}"
                   + $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}";

        HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"API-Fehler {(int)response.StatusCode}: {response.ReasonPhrase}\n{body}");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.MetForecastResponse);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
