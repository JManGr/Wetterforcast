using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WeatherApp.Models;

// ── Met.no locationforecast/2.0/compact JSON-Modelle ────────────────────────

public class MetForecastResponse
{
    [JsonPropertyName("properties")]
    public ForecastProperties Properties { get; set; } = new();
}

public class ForecastProperties
{
    [JsonPropertyName("meta")]
    public Meta Meta { get; set; } = new();

    [JsonPropertyName("timeseries")]
    public List<TimeSeriesItem> Timeseries { get; set; } = [];
}

public class Meta
{
    [JsonPropertyName("units")]
    public Units Units { get; set; } = new();

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class Units
{
    [JsonPropertyName("air_temperature")]
    public string AirTemperature { get; set; } = "C";

    [JsonPropertyName("wind_speed")]
    public string WindSpeed { get; set; } = "m/s";

    [JsonPropertyName("relative_humidity")]
    public string RelativeHumidity { get; set; } = "%";
}

public class TimeSeriesItem
{
    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("data")]
    public TimeSeriesData Data { get; set; } = new();
}

public class TimeSeriesData
{
    [JsonPropertyName("instant")]
    public InstantData Instant { get; set; } = new();

    [JsonPropertyName("next_1_hours")]
    public PeriodSummary? Next1Hours { get; set; }

    [JsonPropertyName("next_6_hours")]
    public PeriodSummary? Next6Hours { get; set; }
}

public class InstantData
{
    [JsonPropertyName("details")]
    public InstantDetails Details { get; set; } = new();
}

public class InstantDetails
{
    [JsonPropertyName("air_temperature")]
    public double AirTemperature { get; set; }

    [JsonPropertyName("relative_humidity")]
    public double RelativeHumidity { get; set; }

    [JsonPropertyName("wind_speed")]
    public double WindSpeed { get; set; }

    [JsonPropertyName("wind_from_direction")]
    public double WindFromDirection { get; set; }
}

public class PeriodSummary
{
    [JsonPropertyName("summary")]
    public SymbolSummary Summary { get; set; } = new();

    [JsonPropertyName("details")]
    public PeriodDetails Details { get; set; } = new();
}

public class PeriodDetails
{
    [JsonPropertyName("precipitation_amount")]
    public double PrecipitationAmount { get; set; }

    [JsonPropertyName("precipitation_amount_max")]
    public double? PrecipitationAmountMax { get; set; }

    [JsonPropertyName("precipitation_amount_min")]
    public double? PrecipitationAmountMin { get; set; }
}

public class SymbolSummary
{
    [JsonPropertyName("symbol_code")]
    public string SymbolCode { get; set; } = string.Empty;
}

// ── Präsentationsmodell für eine einzelne Vorhersage-Karte ──────────────────

public class ForecastCardModel
{
    public string Time { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TempDisplay { get; init; } = string.Empty;
    public string HumidityDisplay { get; init; } = string.Empty;
    public string WindDisplay { get; init; } = string.Empty;
    public string PrecipitationDisplay { get; init; } = string.Empty; // z. B. "0.2 mm"
    public double PrecipitationAmount { get; init; } // für Chart
    public string IconEmoji { get; init; } = "🌡️";
}
