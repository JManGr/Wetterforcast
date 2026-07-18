using Avalonia;
using System;

namespace WeatherApp;

/// <summary>
/// Einstiegspunkt der Anwendung. Startet den Avalonia-UI-Thread.
/// </summary>
class Program
{
    // Hauptthread – muss STA auf Windows sein, Avalonia übernimmt das.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Erstellt und konfiguriert die Avalonia-Anwendung.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()   // Wählt automatisch Windows- oder Linux-Backend
            .WithInterFont()
            .LogToTrace();
}
