using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;

namespace WeatherApp.Models;

/// <summary>
/// Präsentationsmodell für einen einzelnen Tag (Tab).
/// Enthält Karten + Chart-Daten für diesen Tag.
/// </summary>
public class ForecastDayGroup
{
    public DateTime Date { get; init; }
    public string DayLabel { get; init; } = string.Empty;        // z. B. "Heute" / "Di 03.09"
    public string DateLabel { get; init; } = string.Empty;       // z. B. "Dienstag, 03. September"
    public string TempRangeDisplay { get; init; } = string.Empty; // z. B. "12 – 22 °C"
    public string SummaryDisplay { get; init; } = string.Empty;  // z. B. "teils bewölkt"

    public ObservableCollection<ForecastCardModel> Cards { get; init; } = [];

    public ISeries[] TemperatureSeries { get; init; } = [];
    public Axis[] XAxes { get; init; } = [];
    public Axis[] YAxes { get; init; } =
    [
        new Axis
        {
            Name = "Temperatur (°C)",
            NamePaint = new SolidColorPaint(SKColors.LightGray),
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            NameTextSize = 13,
            TextSize = 11,
        }
    ];
}
