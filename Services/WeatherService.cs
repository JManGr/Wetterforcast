using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WeatherService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Met.no erfordert einen aussagekräftigen User-Agent (Domain + Kontakt).
        // TODO: E-Mail-Adresse durch echte Kontaktdaten ersetzen.
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WeatherApp", "1.0"));
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(contact@yourdomain.com)"));
    }

    /// <summary>
    /// Lädt die stündliche Vorhersage für einen geographischen Punkt.
    /// </summary>
    /// <param name="latitude">Breitengrad</param>
    /// <param name="longitude">Längengrad</param>
    /// <returns>Deserialisierte Met.no-Vorhersage</returns>
    public async Task<MetForecastResponse?> GetForecastAsync(double latitude, double longitude)
    {
        string url = "https://api.met.no/weatherapi/locationforecast/2.0/compact"
                   + $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}"
                   + $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}";

        HttpResponseMessage response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"API-Fehler {(int)response.StatusCode}: {response.ReasonPhrase}\n{body}");
        }

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MetForecastResponse>(json, _jsonOptions);
    }

    public void Dispose() => _http.Dispose();
}
