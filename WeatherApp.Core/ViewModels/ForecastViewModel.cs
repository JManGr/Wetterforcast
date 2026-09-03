using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.ViewModels;

/// <summary>
/// 7-Tage-Vorhersage + 4h-Niederschlagsvorschau (met.no).
/// Reiner State + <see cref="ForecastMapper"/> – kein Netzwerk.
/// </summary>
public partial class ForecastViewModel : ObservableObject
{
    public ObservableCollection<ForecastDayGroup> ForecastDays { get; } = [];

    [ObservableProperty]
    private bool _hasForecastDays = false;

    public ObservableCollection<ForecastCardModel> NextHours { get; } = [];

    [ObservableProperty]
    private ISeries[] _precipitationSeries = [];

    [ObservableProperty]
    private Axis[] _precipitationXAxes = [];

    [ObservableProperty]
    private Axis[] _precipitationYAxes =
    [
        new Axis
        {
            Name = "Niederschlag (mm)",
            NamePaint = new SolidColorPaint(SKColors.LightGray),
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            NameTextSize = 12,
            TextSize = 10,
        }
    ];

    /// <summary>Übernimmt eine frische met.no-Antwort atomar ins UI.</summary>
    public void ApplyForecast(MetForecastResponse forecast)
    {
        var newDays = new System.Collections.Generic.List<ForecastDayGroup>();
        foreach (var (date, items) in ForecastMapper.GroupByDay(forecast))
            newDays.Add(ForecastMapper.BuildDayGroup(date, items));

        ForecastDays.Clear();
        foreach (var d in newDays)
            ForecastDays.Add(d);
        HasForecastDays = ForecastDays.Count > 0;

        var nextItems = ForecastMapper.SelectNextHours(forecast);

        NextHours.Clear();
        foreach (var card in ForecastMapper.BuildHourCards(nextItems))
            NextHours.Add(card);

        var (series, xAxes, yMax) = ForecastMapper.BuildPrecipitationData(nextItems);
        PrecipitationSeries = series;
        PrecipitationXAxes = xAxes;
        PrecipitationYAxes[0].MaxLimit = yMax;
    }
}
