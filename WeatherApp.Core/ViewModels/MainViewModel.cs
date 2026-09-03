using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

/// <summary>
/// Koordinator (dünn): Eingabe, Geocoding + met.no-Abruf, Auto-Refresh.
/// Fachlogik liegt in <see cref="ForecastViewModel"/>, <see cref="RadarViewModel"/>,
/// <see cref="OrientationViewModel"/> sowie <see cref="ForecastMapper"/> /
/// <see cref="OrientationService"/> (ehemals God-ViewModel, ~890 Zeilen).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly GeocodingService _geocoder = new();
    private readonly WeatherService _weather = new();
    private readonly CancellationTokenSource _autoRefreshCts = new();
    private readonly Task _autoRefreshTask;
    private bool _disposed;

    public ForecastViewModel Forecast { get; } = new();
    public RadarViewModel Radar { get; }
    public OrientationViewModel Orientation { get; } = new();

    [ObservableProperty]
    private string _city = "Bestwig";

    [ObservableProperty]
    private string _statusMessage = "Stadt eingeben und auf 'Laden' klicken.";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _cityTitle = string.Empty;

    // ── Mehrdeutige Ortsnamen: Kandidaten-Auswahl ─────────────────────────────

    public ObservableCollection<GeocodingResult> LocationCandidates { get; } = [];

    [ObservableProperty]
    private GeocodingResult? _selectedCandidate;

    [ObservableProperty]
    private bool _hasLocationCandidates = false;

    // Zuletzt geladener Ort (für Auto-Refresh ohne erneutes Geocoding)
    private double _lastLat;
    private double _lastLon;
    private string _lastDisplayName = string.Empty;
    private bool _hasLocation;

    public MainViewModel()
    {
        Radar = new RadarViewModel(_autoRefreshCts.Token);
        _autoRefreshTask = RunAutoRefreshAsync();
    }

    partial void OnIsLoadingChanged(bool value) => LoadWeatherCommand.NotifyCanExecuteChanged();

    partial void OnSelectedCandidateChanged(GeocodingResult? value)
    {
        if (value is null || IsLoading || _disposed) return;
        // Auswahl lädt direkt – ohne erneutes Geocoding.
        HasLocationCandidates = false;
        _ = LoadForLocationAsync(value.Latitude, value.Longitude, value.DisplayName, _autoRefreshCts.Token);
    }

    private bool CanLoadWeather() => !IsLoading && !_disposed;

    /// <summary>
    /// Aktualisiert die aktuellen Wetterdaten automatisch alle <see cref="AutoRefreshInterval"/>.
    /// </summary>
    private async Task RunAutoRefreshAsync()
    {
        using var timer = new PeriodicTimer(AutoRefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_autoRefreshCts.Token))
            {
                if (_disposed || IsLoading) continue;
                if (_hasLocation)
                {
                    // Direkt mit gespeichertem Ort – kein erneutes Geocoding.
                    await LoadForLocationAsync(_lastLat, _lastLon, _lastDisplayName, _autoRefreshCts.Token);
                }
                else if (!string.IsNullOrWhiteSpace(City))
                {
                    await LoadWeatherAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartet beim Dispose – ignorieren.
        }
    }

    /// <summary>
    /// Intervall für die automatische Aktualisierung. Standard: 30 Minuten.
    /// </summary>
    public static TimeSpan AutoRefreshInterval { get; } = TimeSpan.FromMinutes(30);

    [RelayCommand(CanExecute = nameof(CanLoadWeather))]
    private async Task LoadWeatherAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(City))
        {
            StatusMessage = "⚠️ Bitte eine Stadt eingeben.";
            return;
        }

        if (IsLoading) return;

        IsLoading = true;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _autoRefreshCts.Token);
        CancellationToken ct = linkedCts.Token;

        try
        {
            // 1. Stadt → Koordinaten (alle Kandidaten für Auswahl-UI)
            StatusMessage = $"Suche Koordinaten für '{City}'…";
            var candidates = await _geocoder.ResolveCandidatesAsync(City, ct);

            if (candidates.Count == 0)
            {
                StatusMessage = $"❌ Stadt '{City}' nicht gefunden!";
                return;
            }

            if (candidates.Count > 1)
            {
                // Mehrdeutig – Nutzer wählt (OnSelectedCandidateChanged lädt dann).
                SelectedCandidate = null;
                LocationCandidates.Clear();
                foreach (var c in candidates) LocationCandidates.Add(c);
                HasLocationCandidates = true;
                StatusMessage = $"❓ {candidates.Count} Treffer für '{City}' – bitte wählen.";
                return;
            }

            HasLocationCandidates = false;
            var location = candidates[0];

            await LoadForLocationCoreAsync(location.Latitude, location.Longitude, location.DisplayName, ct);

        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusMessage = "⏹️ Abgebrochen.";
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("403"))
        {
            StatusMessage = "❌ 403 Forbidden von met.no – User-Agent geblockt. Prüfe Services/WeatherService.cs: User-Agent darf kein example.com enthalten. Details: " + ex.Message.Split('\n')[0];
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

    /// <summary>
    /// Lädt Wetter + Radar + Pins für einen bekannten Ort (Auswahl oder Refresh).
    /// Eigene IsLoading-Sicherung, damit Auswahl und Auto-Refresh direkt einsteigen.
    /// </summary>
    private async Task LoadForLocationAsync(double lat, double lon, string displayName, CancellationToken token)
    {
        if (IsLoading) return;

        IsLoading = true;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            token, _autoRefreshCts.Token);
        CancellationToken ct = linkedCts.Token;

        try
        {
            await LoadForLocationCoreAsync(lat, lon, displayName, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusMessage = "⏹️ Abgebrochen.";
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("403"))
        {
            StatusMessage = "❌ 403 Forbidden von met.no – User-Agent geblockt. Details: " + ex.Message.Split('\n')[0];
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

    private async Task LoadForLocationCoreAsync(double lat, double lon, string displayName, CancellationToken ct)
    {
        CityTitle = displayName;
        _lastLat = lat;
        _lastLon = lon;
        _lastDisplayName = displayName;
        _hasLocation = true;

        // 2. Koordinaten → Wetterdaten
        StatusMessage = $"Lade Wetterdaten für {CityTitle}…";
        MetForecastResponse? forecast = await _weather.GetForecastAsync(lat, lon, ct);

        if (forecast is null || forecast.Properties.Timeseries.Count == 0)
        {
            StatusMessage = "❌ Keine Wetterdaten erhalten.";
            return;
        }

        // 3. Vorhersage atomar übernehmen (reine Mapper-Logik im ForecastViewModel)
        Forecast.ApplyForecast(forecast);

        StatusMessage = $"✅ Aktualisiert: {DateTime.Now:HH:mm:ss} Uhr – {CityTitle} ({Forecast.ForecastDays.Count} Tage)";

        // 4. Radar + Orientierung im Hintergrund (blockiert nicht).
        // WICHTIG: langlebiges Token nutzen – linkedCts wird am Ende
        // dieser Methode disposed und darf nicht weiterleben.
        CancellationToken bgToken = _autoRefreshCts.Token;
        _ = Radar.LoadAsync(lat, lon, CityTitle, bgToken);
        _ = Orientation.LoadAsync(lat, lon, CityTitle, bgToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _autoRefreshCts.Cancel();
            _autoRefreshCts.Dispose();
            Radar.Dispose();
            _geocoder.Dispose();
            _weather.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
