using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

/// <summary>
/// Orientierungs-Pins im 150km-Umkreis (statische Liste via <see cref="OrientationService"/>).
/// </summary>
public partial class OrientationViewModel : ObservableObject
{
    public ObservableCollection<OrientationPin> OrientationPins { get; } = [];

    [ObservableProperty]
    private string _orientationStatus = string.Empty;

    public Task LoadAsync(double lat, double lon, string cityTitle, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OrientationStatus = "Suche Orte 150km… (statische Liste)";
        try
        {
            var (pins, status) = OrientationService.BuildPins(lat, lon, cityTitle);
            OrientationStatus = status;

            ct.ThrowIfCancellationRequested();
            OrientationPins.Clear();
            foreach (var p in pins) OrientationPins.Add(p);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            OrientationStatus = $"Orientierung Fehler: {ex.Message}";
        }
        return Task.CompletedTask;
    }
}
