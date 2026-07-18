using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WeatherApp.Models;

/// <summary>
/// Antwort der Open-Meteo Geocoding-API (kostenlos, ohne API-Key).
/// Wandelt einen Stadtnamen in geographische Koordinaten um.
/// </summary>
public class GeocodingResponse
{
    [JsonPropertyName("results")]
    public List<GeocodingResult> Results { get; set; } = [];
}

public class GeocodingResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("admin1")]
    public string Admin1 { get; set; } = string.Empty;

    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Admin1)
            ? $"{Name}, {Country}"
            : $"{Name}, {Admin1}, {Country}";
}
