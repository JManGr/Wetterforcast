using System;
using WeatherApp.Models;
using WeatherApp.ViewModels;

namespace WeatherApp.Tests;

public class ForecastViewModelTests
{
    [Fact]
    public void ApplyForecast_PopulatesDaysAndHours()
    {
        var forecast = new MetForecastResponse();
        var now = DateTime.UtcNow;
        for (int h = -1; h < 30; h++)
        {
            forecast.Properties.Timeseries.Add(new TimeSeriesItem
            {
                Time = now.AddHours(h),
                Data = new TimeSeriesData
                {
                    Instant = new InstantData
                    {
                        Details = new InstantDetails
                        {
                            AirTemperature = 12 + h * 0.1,
                            RelativeHumidity = 60,
                            WindSpeed = 2,
                            WindFromDirection = 180,
                        }
                    },
                    Next1Hours = new PeriodSummary
                    {
                        Summary = new SymbolSummary { SymbolCode = "fair_day" },
                        Details = new PeriodDetails { PrecipitationAmount = 0 },
                    },
                }
            });
        }

        var vm = new ForecastViewModel();
        vm.ApplyForecast(forecast);

        Assert.True(vm.HasForecastDays);
        Assert.NotEmpty(vm.ForecastDays);
        Assert.True(vm.ForecastDays.Count <= 7);
        Assert.Equal(4, vm.NextHours.Count);
        Assert.Single(vm.PrecipitationSeries);
    }

    [Fact]
    public void ApplyForecast_EmptyForecast_ClearsState()
    {
        var vm = new ForecastViewModel();
        vm.ApplyForecast(new MetForecastResponse());

        Assert.False(vm.HasForecastDays);
        Assert.Empty(vm.ForecastDays);
        Assert.Empty(vm.NextHours);
    }
}
