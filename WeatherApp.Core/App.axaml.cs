using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace WeatherApp;

/// <summary>
/// Avalonia-Anwendungsklasse – initialisiert das Hauptfenster.
/// </summary>
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            // MainView erbt sonst kein DataContext (SingleView hat kein Host-Window)
            single.MainView = new Views.MainView
            {
                DataContext = new ViewModels.MainViewModel()
            };

        base.OnFrameworkInitializationCompleted();
    }
}
