using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using WeatherApp;

namespace WeatherApp.Android;

[Activity(
    Label = "WetterApp",
    MainLauncher = true,
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder)
            .WithInterFont();
}
