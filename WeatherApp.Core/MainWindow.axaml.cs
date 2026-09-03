using Avalonia.Controls;
using System;

namespace WeatherApp;

/// <summary>
/// Code-Behind für das Hauptfenster. Logik liegt im ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable d)
            d.Dispose();
    }
}
