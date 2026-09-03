using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

/// <summary>
/// DWD Niederschlagsradar: Frames, Bild-Cache, Animation, Steuer-Commands.
/// Besitzt <see cref="DwdRadarService"/> selbst und disposed ihn.
/// </summary>
public partial class RadarViewModel : ObservableObject, IDisposable
{
    private readonly DwdRadarService _dwdRadar = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _disposed;

    private double _lastLat;
    private double _lastLon;
    private string _cityTitle = string.Empty;
    private bool _hasLocation;

    private CancellationTokenSource? _radarAnimCts;
    private CancellationTokenSource? _radarImageCts;
    private readonly ConcurrentDictionary<string, byte[]> _radarCacheBytes = new();
    private const int MaxRadarCacheEntries = 30;

    public ObservableCollection<RadarFrameViewModel> RadarFrames { get; } = [];

    [ObservableProperty]
    private int _selectedRadarIndex = -1;

    [ObservableProperty]
    private int _radarMaxIndex = 11;

    [ObservableProperty]
    private string _selectedRadarLabel = string.Empty;

    [ObservableProperty]
    private Bitmap? _radarImage;

    [ObservableProperty]
    private string _radarStatus = "Noch kein Radar geladen – Stadt laden.";

    [ObservableProperty]
    private bool _isRadarLoading = false;

    [ObservableProperty]
    private bool _isRadarPlaying = false;

    [ObservableProperty]
    private string _radarTileUrl = string.Empty;

    /// <param name="lifetimeToken">langlebiges Token (App/Coordinator) für verlinkte CTSs.</param>
    public RadarViewModel(CancellationToken lifetimeToken)
    {
        _lifetimeToken = lifetimeToken;
    }

    partial void OnSelectedRadarIndexChanged(int value)
    {
        if (value >= 0 && value < RadarFrames.Count)
        {
            RadarTileUrl = RadarFrames[value].TileUrl;
            SelectedRadarLabel = RadarFrames[value].Label;
            // Vorherigen Bild-Load abbrechen (Slider-Spamming) – verhindert
            // überlappende GetMap-Requests und späte Fehlzuordnungen.
            _radarImageCts?.Cancel();
            _radarImageCts?.Dispose();
            _radarImageCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
            _ = LoadRadarImageAsync(RadarFrames[value].TileUrl, _radarImageCts.Token);
        }
        else
        {
            SelectedRadarLabel = string.Empty;
        }
    }

    partial void OnIsRadarPlayingChanged(bool value)
    {
        if (value) _ = RunRadarAnimationAsync();
        else _radarAnimCts?.Cancel();
    }

    public async Task LoadAsync(double lat, double lon, string cityTitle, CancellationToken ct)
    {
        _lastLat = lat;
        _lastLon = lon;
        _cityTitle = cityTitle;
        _hasLocation = true;

        IsRadarLoading = true;
        RadarStatus = "Lade DWD Niederschlagsradar…";
        // Laufende Einzelbild-Loads abbrechen – Frames werden neu aufgebaut.
        _radarImageCts?.Cancel();
        try
        {
            var allTimes = await _dwdRadar.GetAvailableTimesAsync(ct);
            if (allTimes.Count == 0)
            {
                RadarStatus = "❌ Keine DWD Radardaten verfügbar.";
                return;
            }

            var now = DateTime.UtcNow;
            // Letzte 2h Vergangenheit + nächste 30 Min (DWD hat 5min Schritte, inkl. Vorhersage) – vollständig 2.5h = 30 Frames
            var window = allTimes.Where(t => t >= now.AddHours(-2) && t <= now.AddMinutes(30)).OrderBy(t => t).ToList();
            if (window.Count < 6)
                window = allTimes.OrderBy(t => t).TakeLast(24).ToList(); // Fallback: letzte 24 Frames (2h)

            // Für Animation 24 Frames (2h) vollständig, 5min Schritte
            var selected = window.TakeLast(24).ToList();

            // Breitenkorrigierte BBOX: Δlon hängt von cos(lat) ab (150 km Radius).
            double dLat = DwdRadarService.GetDeltaLatForKm(150);
            double dLon = DwdRadarService.GetDeltaLonForLat(lat, 150);
            var newFrames = new List<RadarFrameViewModel>(selected.Count);
            foreach (var t in selected)
            {
                string tileUrl = DwdRadarService.BuildMapUrl(lat, lon, t, deltaLat: dLat, deltaLon: dLon);
                string label = t.ToLocalTime().ToString("HH:mm");
                // Markiere Zukunft leicht
                if (t > now) label += " (Prognose)";
                newFrames.Add(new RadarFrameViewModel
                {
                    Time = new DateTimeOffset(t).ToUnixTimeSeconds(),
                    Path = t.ToString("o"),
                    TileUrl = tileUrl,
                    Label = label,
                    DateTimeUtc = t
                });
            }

            _radarCacheBytes.Clear();
            RadarFrames.Clear();
            foreach (var rf in newFrames) RadarFrames.Add(rf);
            RadarMaxIndex = Math.Max(0, RadarFrames.Count - 1);

            // Statisch letztes Bild zeigen, Animation startet später bei 0 (ältestes) für korrekte Reihenfolge
            SelectedRadarIndex = RadarFrames.Count - 1;
            RadarStatus = $"✅ DWD Radar: {RadarFrames.Count} Frames (5 Min, sortiert) – {RadarFrames.First().Label} bis {RadarFrames.Last().Label} – {_cityTitle} – Quelle: DWD Niederschlagsradar 1km";

            if (SelectedRadarIndex >= 0)
                await LoadRadarImageAsync(RadarFrames[SelectedRadarIndex].TileUrl, ct);

            // Hintergrund-Preload aller 24 Frames für flüssige Animation (verhindert 1-2 Frames Problem bei 600ms + 3-6s Fetch)
            _ = PreloadRadarCacheAsync(newFrames, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            RadarStatus = $"❌ DWD Radar-Fehler: {ex.Message}";
        }
        finally
        {
            IsRadarLoading = false;
        }
    }

    private async Task LoadRadarImageAsync(string tileUrl, CancellationToken ct = default)
    {
        bool wasPlaying = IsRadarPlaying;
        if (!wasPlaying) IsRadarLoading = true;
        try
        {
            // Cache hit – sofort anzeigen ohne Netzwerk
            if (_radarCacheBytes.TryGetValue(tileUrl, out var cachedBytes))
            {
                if (SelectedRadarIndex >= 0 && SelectedRadarIndex < RadarFrames.Count && RadarFrames[SelectedRadarIndex].TileUrl != tileUrl)
                    return;
                using var msCached = new MemoryStream(cachedBytes);
                var bmpCached = new Bitmap(msCached);
                var oldCached = RadarImage;
                RadarImage = bmpCached;
                oldCached?.Dispose();
                RadarTileUrl = tileUrl;
                return;
            }

            byte[]? bytes = await _dwdRadar.GetMapBytesAsync(tileUrl, ct);
            if (ct.IsCancellationRequested) return;
            if (bytes == null || bytes.Length == 0)
            {
                if (SelectedRadarIndex >= 0 && SelectedRadarIndex < RadarFrames.Count && RadarFrames[SelectedRadarIndex].TileUrl == tileUrl)
                    RadarStatus = "Radar-Kachel leer (kein Niederschlag in Region?) – " + tileUrl;
                return;
            }
            // Cache für Animation (begrenzt, thread-safe)
            AddToRadarCache(tileUrl, bytes);

            if (SelectedRadarIndex >= 0 && SelectedRadarIndex < RadarFrames.Count && RadarFrames[SelectedRadarIndex].TileUrl != tileUrl)
                return;

            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            var old = RadarImage;
            RadarImage = bmp;
            old?.Dispose();
            RadarTileUrl = tileUrl;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                RadarStatus = $"❌ Radar-Bild Fehler: {ex.Message}";
        }
        finally
        {
            if (!wasPlaying) IsRadarLoading = false;
        }
    }

    private void AddToRadarCache(string tileUrl, byte[] bytes)
    {
        if (_radarCacheBytes.Count >= MaxRadarCacheEntries)
        {
            // Einfachste LRU-Näherung: ältesten Eintrag verwerfen.
            foreach (var key in _radarCacheBytes.Keys)
            {
                _radarCacheBytes.TryRemove(key, out _);
                break;
            }
        }
        _radarCacheBytes[tileUrl] = bytes;
    }

    private async Task PreloadRadarCacheAsync(List<RadarFrameViewModel> frames, CancellationToken ct)
    {
        foreach (var f in frames)
        {
            if (ct.IsCancellationRequested) break;
            if (_radarCacheBytes.ContainsKey(f.TileUrl)) continue;
            try
            {
                var bytes = await _dwdRadar.GetMapBytesAsync(f.TileUrl, ct);
                if (bytes != null && bytes.Length > 0 && bytes[0] != (byte)'<')
                    AddToRadarCache(f.TileUrl, bytes);
            }
            catch (OperationCanceledException) { break; }
            catch { }
            try { await Task.Delay(200, ct); } catch { break; }
        }
        if (!ct.IsCancellationRequested && RadarFrames.Count > 0)
            RadarStatus = $"✅ DWD Radar: {RadarFrames.Count} Frames gecached – {RadarFrames.First().Label} bis {RadarFrames.Last().Label}";
    }

    private async Task RunRadarAnimationAsync()
    {
        _radarAnimCts?.Cancel();
        _radarAnimCts?.Dispose();
        _radarAnimCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        var ct = _radarAnimCts.Token;
        try
        {
            while (!ct.IsCancellationRequested && IsRadarPlaying && RadarFrames.Count > 0)
            {
                await Task.Delay(900, ct);
                if (ct.IsCancellationRequested) break;
                int next = (SelectedRadarIndex + 1) % RadarFrames.Count;
                SelectedRadarIndex = next;
            }
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void ToggleRadarPlay()
    {
        if (RadarFrames.Count == 0) return;
        if (!IsRadarPlaying)
        {
            // Für korrekte Reihenfolge immer beim ältesten Frame starten (0 = älteste Vergangenheit)
            if (SelectedRadarIndex < 0 || SelectedRadarIndex == RadarFrames.Count - 1)
                SelectedRadarIndex = 0;
            IsRadarPlaying = true;
        }
        else
        {
            IsRadarPlaying = false;
        }
    }

    [RelayCommand]
    private void RadarNext()
    {
        if (RadarFrames.Count == 0) return;
        SelectedRadarIndex = (SelectedRadarIndex + 1) % RadarFrames.Count;
    }

    [RelayCommand]
    private void RadarPrev()
    {
        if (RadarFrames.Count == 0) return;
        SelectedRadarIndex = (SelectedRadarIndex - 1 + RadarFrames.Count) % RadarFrames.Count;
    }

    [RelayCommand]
    private async Task RefreshRadarAsync()
    {
        if (!_hasLocation) { RadarStatus = "⚠️ Erst Stadt laden."; return; }
        await LoadAsync(_lastLat, _lastLon, _cityTitle, _lifetimeToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _radarAnimCts?.Cancel();
            _radarAnimCts?.Dispose();
            _radarImageCts?.Cancel();
            _radarImageCts?.Dispose();
            _dwdRadar.Dispose();
            RadarImage?.Dispose();
            _radarCacheBytes.Clear();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
