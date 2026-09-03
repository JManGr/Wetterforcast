using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;

namespace WeatherApp.Services;

/// <summary>
/// Löst Stadtnamen über die kostenlose Open-Meteo Geocoding-API
/// in geographische Koordinaten (lat/lon) auf.
/// </summary>
public class GeocodingService : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    // AOT/Trim kompatibel via AppJsonContext (IL2026)

    private static readonly HttpClient _sharedClient = CreateSharedClient();

    private static HttpClient CreateSharedClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherApp/1.0 (https://github.com/Wetterforcast)");
        return client;
    }

    public GeocodingService() : this(_sharedClient, ownsClient: false) { }

    /// <summary>Für Tests injizierbar.</summary>
    public GeocodingService(HttpClient httpClient, bool ownsClient = true)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Sucht alle Treffer (max. 5) für einen Stadtnamen – für die Auswahl-UI.
    /// </summary>
    public async Task<IReadOnlyList<GeocodingResult>> ResolveCandidatesAsync(string city, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("Stadt darf nicht leer sein.", nameof(city));

        string url = $"https://geocoding-api.open-meteo.com/v1/search"
                   + $"?name={Uri.EscapeDataString(city)}"
                   + "&count=5"
                   + "&language=de"
                   + "&format=json";

        string json = await _http.GetStringAsync(url, cancellationToken);
        GeocodingResponse? result = JsonSerializer.Deserialize(json, AppJsonContext.Default.GeocodingResponse);

        // Results kann null sein (kein Treffer).
        if (result?.Results is not { Count: > 0 })
            return Array.Empty<GeocodingResult>();

        return result.Results;
    }

    /// <summary>
    /// Sucht den besten Treffer für einen Stadtnamen (Komfort-Methode).
    /// Bei mehrdeutigen Namen <see cref="ResolveCandidatesAsync"/> nutzen.
    /// </summary>
    public async Task<GeocodingResult?> ResolveAsync(string city, CancellationToken cancellationToken = default)
    {
        var candidates = await ResolveCandidatesAsync(city, cancellationToken);
        return candidates.Count > 0 ? candidates[0] : null;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
