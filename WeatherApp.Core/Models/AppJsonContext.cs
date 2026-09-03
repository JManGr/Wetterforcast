using System.Text.Json.Serialization;
using WeatherApp.Services;

namespace WeatherApp.Models;

[JsonSerializable(typeof(GeocodingResponse))]
[JsonSerializable(typeof(MetForecastResponse))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
