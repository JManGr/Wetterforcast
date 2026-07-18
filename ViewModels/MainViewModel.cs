using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

/// <summary>
/// ViewModel für das Hauptfenster (MVVM-Pattern via CommunityToolkit).
/// Koordiniert Datenabruf, Diagramm-Daten und Fehlermeldungen.
/// Verwendet met.no für Wetterdaten und Open-Meteo für Geocoding (beides kostenlos).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly GeocodingService _geocoder = new();
    private readonly WeatherService _weather = new();
    private readonly CancellationTokenSource _autoRefreshCts = new();
    private readonly Task _autoRefreshTask;

    // ── Eingabefeld ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _city = "Bestwig";

    // ── Status ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _statusMessage = "Stadt eingeben und auf 'Laden' klicken.";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _cityTitle = string.Empty;

    // ── Vorhersage-Karten (heutiger Tag) ─────────────────────────────────────

    public ObservableCollection<ForecastCardModel> TodayCards { get; } = [];

    // ── Diagramm-Serien ──────────────────────────────────────────────────────

    [ObservableProperty]
    private ISeries[] _temperatureSeries = [];

    [ObservableProperty]
    private Axis[] _xAxes = [];

    [ObservableProperty]
    private Axis[] _yAxes =
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
    private bool disposedValue;

    public MainViewModel()
    {
        _autoRefreshTask = RunAutoRefreshAsync();
    }

    /// <summary>
    /// Aktualisiert die aktuellen Wetterdaten automatisch alle <see cref="AutoRefreshInterval"/>.
    /// </summary>
    private async Task RunAutoRefreshAsync()
    {
        using var timer = new PeriodicTimer(AutoRefreshInterval);

        while (await timer.WaitForNextTickAsync(_autoRefreshCts.Token))
        {
            if (!string.IsNullOrWhiteSpace(City))
            {
                await LoadWeatherAsync();
            }
        }
    }

    /// <summary>
    /// Intervall für die automatische Aktualisierung. Standard: 30 Minuten.
    /// </summary>
    public static TimeSpan AutoRefreshInterval { get; } = TimeSpan.FromMinutes(30);

    // ── Befehl: Wetterdaten laden ────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadWeatherAsync()
    {
        if (string.IsNullOrWhiteSpace(City))
        {
            StatusMessage = "⚠️ Bitte eine Stadt eingeben.";
            return;
        }

        IsLoading = true;
        TodayCards.Clear();

        try
        {
            // 1. Stadt → Koordinaten
            StatusMessage = $"Suche Koordinaten für '{City}'…";
            GeocodingResult? location = await _geocoder.ResolveAsync(City);

            if (location is null)
            {
                StatusMessage = $"❌ Stadt '{City}' nicht gefunden!";
                return;
            }

            CityTitle = location.DisplayName;

            // 2. Koordinaten → Wetterdaten
            StatusMessage = $"Lade Wetterdaten für {CityTitle}…";
            MetForecastResponse? forecast = await _weather.GetForecastAsync(
                location.Latitude, location.Longitude);

            if (forecast is null || forecast.Properties.Timeseries.Count == 0)
            {
                StatusMessage = "❌ Keine Wetterdaten erhalten.";
                return;
            }

            // Nur heutige Einträge anzeigen (UTC-Zeiten aus API in lokale Zeit)
            DateTime todayLocal = DateTime.Now.Date;
            List<TimeSeriesItem> todayItems = forecast.Properties.Timeseries
                .Where(i => i.Time.ToLocalTime().Date == todayLocal)
                .ToList();

            if (todayItems.Count == 0)
            {
                // Fallback: nächsten 24 Stunden nehmen
                todayItems = forecast.Properties.Timeseries.Take(24).ToList();
            }

            // Vorhersage-Karten befüllen
            foreach (TimeSeriesItem item in todayItems)
            {
                string symbolCode = item.Data.Next1Hours?.Summary.SymbolCode
                                 ?? item.Data.Next6Hours?.Summary.SymbolCode
                                 ?? "fair_day";

                TodayCards.Add(new ForecastCardModel
                {
                    Time = item.Time.ToLocalTime().ToString("HH:mm"),
                    Description = MapSymbolToDescription(symbolCode),
                    TempDisplay = $"{item.Data.Instant.Details.AirTemperature:F1} °C",
                    HumidityDisplay = $"💧 {item.Data.Instant.Details.RelativeHumidity:F0} %",
                    WindDisplay = $"💨 {item.Data.Instant.Details.WindSpeed:F1} m/s",
                    IconEmoji = MapSymbolToEmoji(symbolCode)
                });
            }

            // Diagramm aufbauen
            BuildChart(todayItems);

            StatusMessage = $"✅ Aktualisiert: {DateTime.Now:HH:mm:ss} Uhr";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Fehler: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Diagramm-Daten erzeugen ───────────────────────────────────────────────

    private void BuildChart(List<TimeSeriesItem> items)
    {
        ObservableValue[] tempValues = items
            .Select(i => new ObservableValue(Math.Round(i.Data.Instant.Details.AirTemperature, 1)))
            .ToArray();

        string[] labels = items.Select(i => i.Time.ToLocalTime().ToString("HH:mm")).ToArray();

        TemperatureSeries =
        [
            new LineSeries<ObservableValue>
            {
                Name = "Temperatur (°C)",
                Values = tempValues,
                Stroke = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5), 2),
                Fill = new LinearGradientPaint(
                    new[] { new SKColor(0x42, 0xA5, 0xF5, 80), new SKColor(0x42, 0xA5, 0xF5, 0) },
                    new SKPoint(0, 0), new SKPoint(0, 1)),
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(new SKColor(0x42, 0xA5, 0xF5)),
                GeometryStroke = new SolidColorPaint(SKColors.White, 1),
                LineSmoothness = 0.5
            }
        ];

        XAxes =
        [
            new Axis
            {
                Name = "Uhrzeit",
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                Labels = labels,
                NameTextSize = 13,
                TextSize = 11,
            }
        ];
    }

    // ── Hilfsmethoden: Met.no Symbol-Codes ───────────────────────────────────

    /// <summary>Wandelt Met.no Symbol-Codes in passende Emojis um.</summary>
    private static string MapSymbolToEmoji(string code) => code.ToLowerInvariant() switch
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
    private static string MapSymbolToDescription(string code) => code.ToLowerInvariant() switch
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: Verwalteten Zustand (verwaltete Objekte) bereinigen
            }

            // TODO: Nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben und Finalizer überschreiben
            // TODO: Große Felder auf NULL setzen
            disposedValue = true;
        }
    }

    // // TODO: Finalizer nur überschreiben, wenn "Dispose(bool disposing)" Code für die Freigabe nicht verwalteter Ressourcen enthält
    // ~MainViewModel()
    // {
    //     // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
