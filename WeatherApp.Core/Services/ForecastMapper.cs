using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using WeatherApp.Models;

namespace WeatherApp.Services;

/// <summary>
/// Reine Mapping-Logik für met.no-Vorhersagen (kein Netzwerk, kein UI-State).
/// Ausgelagert aus dem ehemaligen God-ViewModel – separat testbar.
/// </summary>
public static class ForecastMapper
{
    /// <summary>Sichere UTC→Lokal-Konvertierung (met.no liefert UTC).</summary>
    public static DateTime ToLocal(DateTime dt)
        => dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime()
            : dt.ToLocalTime();

    /// <summary>Gruppiert Timeseries nach lokalem Tag – bis zu 7 Tage.</summary>
    public static List<(DateTime Date, List<TimeSeriesItem> Items)> GroupByDay(MetForecastResponse forecast)
    {
        var grouped = forecast.Properties.Timeseries
            .Select(i => (Item: i, Local: ToLocal(i.Time)))
            .GroupBy(x => x.Local.Date)
            .OrderBy(g => g.Key)
            .Take(7)
            .ToList();

        var result = grouped
            .Select(g => (g.Key, g.OrderBy(x => x.Local).Select(x => x.Item).ToList()))
            .ToList();

        if (result.Count == 0)
        {
            var fallback = forecast.Properties.Timeseries.Take(24).ToList();
            if (fallback.Count > 0)
                result.Add((ToLocal(fallback[0].Time).Date, fallback));
        }

        return result;
    }

    /// <summary>Nächste 4 Stunden ab jetzt (-30 Min Toleranz).</summary>
    public static List<TimeSeriesItem> SelectNextHours(MetForecastResponse forecast)
    {
        var nowLocal = DateTime.Now;
        var nextItems = forecast.Properties.Timeseries
            .Where(i => ToLocal(i.Time) >= nowLocal.AddMinutes(-30))
            .OrderBy(i => ToLocal(i.Time))
            .Take(4)
            .ToList();
        if (nextItems.Count < 4)
            nextItems = forecast.Properties.Timeseries.Take(4).ToList();
        return nextItems;
    }

    /// <summary>Baut Stunden-Karten (4h-Vorschau im Radar-Tab).</summary>
    public static List<ForecastCardModel> BuildHourCards(List<TimeSeriesItem> items)
    {
        var cards = new List<ForecastCardModel>(items.Count);
        foreach (var item in items)
        {
            string sym = item.Data.Next1Hours?.Summary.SymbolCode
                      ?? item.Data.Next6Hours?.Summary.SymbolCode
                      ?? "fair_day";
            double precip = item.Data.Next1Hours?.Details.PrecipitationAmount
                         ?? item.Data.Next6Hours?.Details.PrecipitationAmount
                         ?? 0;
            cards.Add(new ForecastCardModel
            {
                Time = ToLocal(item.Time).ToString("HH:mm"),
                Description = MapSymbolToDescription(sym),
                TempDisplay = $"{item.Data.Instant.Details.AirTemperature:F1} °C",
                HumidityDisplay = $"💧 {item.Data.Instant.Details.RelativeHumidity:F0} %",
                WindDisplay = $"💨 {item.Data.Instant.Details.WindSpeed:F1} m/s",
                PrecipitationDisplay = $"🌧️ {precip:F1} mm",
                PrecipitationAmount = precip,
                IconEmoji = MapSymbolToEmoji(sym)
            });
        }
        return cards;
    }

    /// <summary>Baut Niederschlags-Säulendiagramm + Y-Limit.</summary>
    public static (ISeries[] Series, Axis[] XAxes, double YMaxLimit) BuildPrecipitationData(List<TimeSeriesItem> items)
    {
        double[] precipValues = items
            .Select(i => i.Data.Next1Hours?.Details.PrecipitationAmount
                      ?? i.Data.Next6Hours?.Details.PrecipitationAmount
                      ?? 0)
            .ToArray();
        string[] labels = items.Select(i => ToLocal(i.Time).ToString("HH:mm")).ToArray();

        ISeries[] series =
        [
            new ColumnSeries<double>
            {
                Name = "Niederschlag (mm)",
                Values = precipValues,
                Stroke = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5), 1),
                Fill = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                Rx = 4,
                Ry = 4,
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                Name = "Uhrzeit",
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                Labels = labels,
                NameTextSize = 12,
                TextSize = 10,
            }
        ];

        double max = precipValues.Length > 0 ? precipValues.Max() : 1;
        double yMax = max < 1 ? 1 : Math.Ceiling(max + 0.5);
        return (series, xAxes, yMax);
    }

    /// <summary>Baut ein ForecastDayGroup für einen einzelnen Tag.</summary>
    public static ForecastDayGroup BuildDayGroup(DateTime date, List<TimeSeriesItem> items)
    {
        var culture = new CultureInfo("de-DE");
        DateTime todayLocal = DateTime.Now.Date;

        string dayLabel;
        if (date == todayLocal)
            dayLabel = $"Heute {date:dd.MM}";
        else if (date == todayLocal.AddDays(1))
            dayLabel = $"Morgen {date:dd.MM}";
        else
            dayLabel = $"{culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek)} {date:dd.MM}";

        string dateLabel = $"{culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetDayName(date.DayOfWeek))}, {date:dd. MMMM}";

        var cards = new ObservableCollection<ForecastCardModel>();
        foreach (TimeSeriesItem item in items)
        {
            string symbolCode = item.Data.Next1Hours?.Summary.SymbolCode
                             ?? item.Data.Next6Hours?.Summary.SymbolCode
                             ?? "fair_day";
            double precip = item.Data.Next1Hours?.Details.PrecipitationAmount
                         ?? item.Data.Next6Hours?.Details.PrecipitationAmount
                         ?? 0;

            cards.Add(new ForecastCardModel
            {
                Time = ToLocal(item.Time).ToString("HH:mm"),
                Description = MapSymbolToDescription(symbolCode),
                TempDisplay = $"{item.Data.Instant.Details.AirTemperature:F1} °C",
                HumidityDisplay = $"💧 {item.Data.Instant.Details.RelativeHumidity:F0} %",
                WindDisplay = $"💨 {item.Data.Instant.Details.WindSpeed:F1} m/s",
                PrecipitationDisplay = $"🌧️ {precip:F1} mm",
                PrecipitationAmount = precip,
                IconEmoji = MapSymbolToEmoji(symbolCode)
            });
        }

        var tempValues = items
            .Select(i => new ObservableValue(Math.Round(i.Data.Instant.Details.AirTemperature, 1)))
            .ToArray();
        var labels = items.Select(i => ToLocal(i.Time).ToString("HH:mm")).ToArray();

        double minT = items.Min(i => i.Data.Instant.Details.AirTemperature);
        double maxT = items.Max(i => i.Data.Instant.Details.AirTemperature);
        string tempRange = $"{minT:F0} – {maxT:F0} °C";

        string dominantSymbol = items
            .Select(i => i.Data.Next1Hours?.Summary.SymbolCode ?? i.Data.Next6Hours?.Summary.SymbolCode ?? "fair_day")
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "fair_day";
        string summary = MapSymbolToDescription(dominantSymbol);

        var series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Name = "Temperatur (°C)",
                Values = tempValues,
                Stroke = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5), 2),
                Fill = new LinearGradientPaint(
                    new[] { new SKColor(0x42, 0xA5, 0xF5, 80), new SKColor(0x42, 0xA5, 0xF5, 0) },
                    new SKPoint(0, 0), new SKPoint(0, 1)),
                GeometrySize = 7,
                GeometryFill = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                GeometryStroke = new SolidColorPaint(SKColors.White, 1),
                LineSmoothness = 0.5
            }
        };

        var xAxes = new Axis[]
        {
            new Axis
            {
                Name = "Uhrzeit",
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                Labels = labels,
                NameTextSize = 12,
                TextSize = 10,
            }
        };

        return new ForecastDayGroup
        {
            Date = date,
            DayLabel = dayLabel,
            DateLabel = dateLabel,
            TempRangeDisplay = tempRange,
            SummaryDisplay = summary,
            Cards = cards,
            TemperatureSeries = series,
            XAxes = xAxes,
            YAxes =
            [
                new Axis
                {
                    Name = "Temperatur (°C)",
                    NamePaint = new SolidColorPaint(SKColors.LightGray),
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    NameTextSize = 12,
                    TextSize = 10,
                }
            ]
        };
    }

    /// <summary>Wandelt Met.no Symbol-Codes in passende Emojis um.</summary>
    public static string MapSymbolToEmoji(string code) => code.ToLowerInvariant() switch
    {
        var s when s.Contains("clearsky") => "☀️",
        var s when s.Contains("fair") => "🌤️",
        var s when s.Contains("partlycloudy") => "⛅",
        var s when s.Contains("cloudy") => "☁️",
        var s when s.Contains("rain") && s.Contains("thunder") => "⛈️",
        var s when s.Contains("rain") && s.Contains("snow") => "🌨️",
        var s when s.Contains("rain") => "🌧️",
        var s when s.Contains("sleet") => "🌨️",
        var s when s.Contains("snow") => "❄️",
        var s when s.Contains("fog") => "🌫️",
        _ => "🌡️"
    };

    /// <summary>Liefert eine vereinfachte deutsche Beschreibung des Symbolcodes.</summary>
    public static string MapSymbolToDescription(string code) => code.ToLowerInvariant() switch
    {
        var s when s.Contains("clearsky") => "Klar",
        var s when s.Contains("fair") => "Heiter",
        var s when s.Contains("partlycloudy") => "Teils bewölkt",
        var s when s.Contains("cloudy") => "Bewölkt",
        var s when s.Contains("rain") && s.Contains("thunder") => "Gewitter",
        var s when s.Contains("rain") && s.Contains("snow") => "Regen/Schnee",
        var s when s.Contains("rain") => "Regen",
        var s when s.Contains("sleet") => "Schneeregen",
        var s when s.Contains("snow") => "Schnee",
        var s when s.Contains("fog") => "Nebel",
        _ => "Wetter"
    };
}
