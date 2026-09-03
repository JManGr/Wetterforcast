# Wettervorhersage-App

Eine grafische C#-Anwendung zur Anzeige der 7-Tage Wettervorhersage + DWD Regenradar (150km, 5min, 1km).
Läuft auf **Windows, Linux (Avalonia)** und **Android** (`net10.0` LTS, `net10.0-android`).

Wetterdaten: **[met.no](https://api.met.no/weatherapi/locationforecast/2.0/compact)** + **[maps.dwd.de](https://maps.dwd.de/geoserver/ows)** WMS, Geocoding **[Open-Meteo](https://geocoding-api.open-meteo.com)**.

## Features
- 7-Tage Vorhersage Tabs + stündliche Karten (Emoji, Temp, Niederschlag, Wind)
- DWD Niederschlagsradar 512×512, 150km Radius, 2h Vergangenheit + 30min Prognose, Play-Animation
- Orientierungs-Pins 150km zweistufig: `>100k` zuerst, dann `30-100k` nach Abstand, Label-Kollisionsfilter (8px Toleranz)
- Temperatur-/Niederschlag-Diagramme (LiveChartsCore)
- Asynchroner HttpClient, 30min Auto-Refresh, Fehlerhandling

## Voraussetzungen
| Tool | Mindestversion | Hinweis |
|------|----------------|---------|
| .NET SDK | 10.0.400 | LTS bis 11/2028, `dotnet --version` |
| WSL | WSLg (Win11) | für Linux-Test: `dotnet run -f net10.0` |
| Android | API 24+ | `dotnet workload install android` für APK |

Kein API-Key. User-Agent `WeatherApp/1.0 (https://github.com/Wetterforcast)` – `example.com` wird von met.no mit 403 geblockt.

## Bauen & Starten
```bash
# Desktop Windows
dotnet run --project WeatherApp/WeatherApp.csproj -f net10.0
dotnet publish WeatherApp/WeatherApp.csproj -c Release -r win-x64 --self-contained -f net10.0

# Desktop Linux / WSL (WSLg Wayland)
dotnet run --project WeatherApp/WeatherApp.csproj -f net10.0
# Falls Fehler NETSDK1147 (android Workload): nur net10.0 bauen
dotnet build WeatherApp.Core/WeatherApp.Core.csproj -f net10.0
# oder Workload installieren
sudo dotnet workload install android

# Android (API 24+, 16KB Warnung SkiaSharp 2.88.9 nur Warnung)
dotnet publish WeatherApp.Android2/WeatherApp.Android.csproj -c Release -f net10.0-android
# APK: WeatherApp.Android2/bin/Release/net10.0-android/publish/com.jman.WeatherApp.Android-Signed.apk
# Install: adb install -r publish/*.apk; adb shell am start -n com.jman.WeatherApp.Android/crc64eb1a31aac7d82594.MainActivity
```

Visual Studio: `WeatherApp.slnx` öffnen, F5 (enthält `WeatherApp.Core`, `WeatherApp`, `WeatherApp.Android2`).

## APIs
| Zweck | API |
|-------|-----|
| Stadt → Koordinaten | https://geocoding-api.open-meteo.com/v1/search |
| Wettervorhersage | https://api.met.no/weatherapi/locationforecast/2.0/compact |
| DWD Radar | https://maps.dwd.de/geoserver/ows?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=dwd:Niederschlagsradar&CRS=EPSG:4326&BBOX=...&TIME=... |
| Orientierung | statische Fallback-Liste (150km, zweistufig) via OrientationService |

## Projektstruktur
```
WeatherApp.Core/              # Shared Library net10.0;net10.0-android
├── Models/                   # WeatherModels, GeocodingModels, OrientationPin, AppJsonContext (IL2026)
├── Services/                 # WeatherService, GeocodingService, DwdRadarService, ForecastMapper, OrientationService
├── ViewModels/MainViewModel.cs # 150km zweistufig, Label-Overlap, Dwd Delta150km 1.35/2.15
├── Views/MainView.axaml      # UserControl für Desktop+Android
├── MainWindow.axaml          # Window (Desktop) Icon avares://WeatherApp.Core/Assets/weather-icon.png
└── Assets/weather-icon.png/.ico

WeatherApp/                   # Desktop Host net10.0 WinExe
├── Program.cs                # BuildAvaloniaApp .WithInterFont()
└── Assets/weather-icon.ico   # ApplicationIcon

WeatherApp.Android2/          # Android net10.0-android
├── MainActivity.cs           # AvaloniaMainActivity<App>
├── AndroidManifest.xml       # INTERNET
└── Resources/mipmap-*/appicon.png # Wetter-Icon 48-192

WeatherApp.slnx
LICENSE (MIT) + ThirdPartyNotices.txt
```

## NuGet
| Paket | Zweck | Version |
|-------|-------|---------|
| Avalonia (+Desktop/Android/Themes.Fluent/Fonts.Inter) | Cross-platform GUI | 11.3.7 |
| LiveChartsCore.SkiaSharpView.Avalonia | Diagramme | 2.1.0-dev-798 (2.0.5 stable crasht mit Avalonia 12: PinchEvent) |
| CommunityToolkit.Mvvm | MVVM | 8.4.2 |
| Tmds.DBus.Protocol | Linux D-Bus (transitiv) | 0.95.0 (fix GHSA-xrw6) |
| SkiaSharp 2.88.9 | Rendering – XA0141 16KB Warnung, 4KB Geräte OK |

Lizenzen: `LICENSE` MIT (eigener Code), `ThirdPartyNotices.txt` MIT + DWD/met.no CC BY 4.0 / OSM ODbL – Attribution in UI vorhanden.
