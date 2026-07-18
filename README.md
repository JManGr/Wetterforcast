# Wettervorhersage-App

Eine grafische C#-Anwendung zur Anzeige der heutigen Wettervorhersage.  
Läuft auf **Windows und Linux** dank [Avalonia UI](https://avaloniaui.net/).

Wetterdaten kommen von der kostenlosen **[met.no](https://www.met.no/) API**, keine Registrierung oder API-Schlüssel erforderlich.

## Features

- Temperaturgraph (LiveChartsCore) für den heutigen Tag
- Stündliche Vorhersage-Karten mit Emoji, Temperatur, Luftfeuchtigkeit & Wind
- Asynchroner Datenabruf via `HttpClient` (kein UI-Einfrieren)
- Fehlerbehandlung für Netzwerkprobleme oder unbekannte Stadt
- Dunkles Fluent-Design

## Voraussetzungen

| Tool | Mindestversion |
|------|----------------|
| .NET SDK | 8.0 |

Kein API-Key nötig. Die Stadt wird über die kostenlose [Open-Meteo Geocoding API](https://open-meteo.com/en/docs/geocoding-api) in Koordinaten umgewandelt.

## Bauen & Starten

```bash
# Abhängigkeiten wiederherstellen
dotnet restore WeatherApp/WeatherApp.csproj

# Debug-Build starten
dotnet run --project WeatherApp/WeatherApp.csproj

# Release-Build erzeugen (Windows)
dotnet publish WeatherApp/WeatherApp.csproj -c Release -r win-x64 --self-contained

# Release-Build erzeugen (Linux)
dotnet publish WeatherApp/WeatherApp.csproj -c Release -r linux-x64 --self-contained
```

## Starten in Visual Studio

Die Datei `WeatherApp.slnx` im Projektordner öffnen und **F5** drücken.

## Benutzung

1. App starten (z. B. mit `dotnet run` oder per F5 in Visual Studio)
2. **Stadt** eingeben (z. B. `Bestwig`)
3. Auf **🔍 Laden** klicken

## APIs

| Zweck | API |
|-------|-----|
| Stadt → Koordinaten | https://geocoding-api.open-meteo.com/v1/search |
| Wettervorhersage | https://api.met.no/weatherapi/locationforecast/2.0/compact |

Bitte beachte, dass `met.no` einen aussagekräftigen User-Agent verlangt. Die App sendet `WeatherApp/1.0 (demo@example.com)`.

## Projektstruktur

```
WeatherApp/
├── Models/
│   ├── WeatherModels.cs         # Met.no JSON-Modelle + Karten-Modell
│   └── GeocodingModels.cs       # Open-Meteo Geocoding-Modelle
├── Services/
│   ├── WeatherService.cs        # Met.no HTTP-Abruf
│   └── GeocodingService.cs      # City → lat/lon
├── ViewModels/
│   └── MainViewModel.cs         # MVVM-ViewModel (CommunityToolkit)
├── App.axaml / App.axaml.cs     # Avalonia-Anwendungsklasse
├── MainWindow.axaml              # UI-Layout (XAML)
├── MainWindow.axaml.cs           # Code-Behind (minimal)
├── Program.cs                    # Einstiegspunkt
├── WeatherApp.csproj             # NuGet-Pakete & Build-Konfiguration
└── WeatherApp.slnx               # Visual Studio Solution
```

## Verwendete NuGet-Pakete

| Paket | Zweck |
|-------|-------|
| `Avalonia` + `Avalonia.Desktop` | Cross-platform GUI |
| `Avalonia.Themes.Fluent` | Modernes Dark-Theme |
| `LiveChartsCore.SkiaSharpView.Avalonia` | Temperatur-Liniendiagramm |
| `CommunityToolkit.Mvvm` | MVVM-Bindings & Commands |
