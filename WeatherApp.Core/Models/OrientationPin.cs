using Avalonia;
using System;

namespace WeatherApp.Models;

/// <summary>
/// Pin für 100km Umkreis auf dem DWD Radar-Tile (512x512, BBOX ±1°).
/// X/Y sind Pixel-Koordinaten im 512x512 Referenz-Canvas.
/// </summary>
public class OrientationPin
{
    public string Name { get; init; } = string.Empty;
    public string PlaceType { get; init; } = string.Empty; // city/town/village
    public double Lat { get; init; }
    public double Lon { get; init; }
    public double DistanceKm { get; init; }
    public double X { get; init; } // 0..512 (0 = West, 256 = Zentrum, 512 = Ost)
    public double Y { get; init; } // 0..512 (0 = Nord/oben, 256 = Zentrum, 512 = Süd/unten)
    public string DisplayLabel => $"{Name} ({DistanceKm:F0} km)";
    // Canvas-Position für 9x9 Ellipse zentriert auf X/Y (Top-Left = X-4.5, Y-4.5)
    // PinMargin bleibt für Fallback (Grid-Margin-Hack), wird aber nicht mehr für Canvas verwendet
    public Thickness PinMargin => new Thickness(X - 4.5, Y - 4.5, 0, 0);
    public double CanvasLeft => X - 4.5;
    public double CanvasTop => Y - 4.5;
    // Label-Overlap: nur anzeigen wenn keine/geringe Überdeckung (wird im ViewModel gesetzt)
    public bool ShowLabel { get; init; } = true;
    public bool HideLabel => !ShowLabel;
}
