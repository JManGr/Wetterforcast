using System;

namespace WeatherApp.Models;

// ── View-Modell für ein einzelnes DWD-Radar-Frame (Tile) ────────────────────
// (Datei hieß früher RainViewerModels.cs – RainViewer-spezifische DTOs
// RainViewerResponse/RadarInfo/SatelliteInfo/RadarFrame wurden entfernt,
// da nur noch DWD via DwdRadarService genutzt wird.)

public class RadarFrameViewModel
{
    public long Time { get; init; }
    public string Path { get; init; } = string.Empty;
    public string TileUrl { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public DateTime DateTimeUtc { get; init; }
}
