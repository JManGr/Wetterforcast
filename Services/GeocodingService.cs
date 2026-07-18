using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeocodingService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WeatherApp", "1.0"));
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(contact@yourdomain.com)"));
    }

    /// <summary>
    /// Sucht den ersten Treffer für einen Stadtnamen.
    /// </summary>
    public async Task<GeocodingResult?> ResolveAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("Stadt darf nicht leer sein.", nameof(city));

        string url = $"https://geocoding-api.open-meteo.com/v1/search"
                   + $"?name={Uri.EscapeDataString(city)}"
                   + "&count=5"
                   + "&language=de"
                   + "&format=json";

        string json = await _http.GetStringAsync(url);
        GeocodingResponse? result = JsonSerializer.Deserialize<GeocodingResponse>(json, _jsonOptions);

        if (result?.Results is { Count: 0 })
            return null;

        return result!.Results[0];
    }

    public void Dispose() => _http.Dispose();
}
