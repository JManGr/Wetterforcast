using System;
using System.Collections.Generic;
using System.Linq;
using WeatherApp.Models;
using WeatherApp.Services;

namespace WeatherApp.Tests;

public class ForecastMapperTests
{
    [Theory]
    [InlineData("clearsky_day", "☀️")]
    [InlineData("clearsky_night", "☀️")]
    [InlineData("fair_day", "🌤️")]
    [InlineData("partlycloudy_day", "⛅")]
    [InlineData("cloudy", "☁️")]
    [InlineData("lightrainshowers_day", "🌧️")]
    [InlineData("heavyrainandthunder", "⛈️")]
    [InlineData("rainandthunder", "⛈️")]
    [InlineData("lightsnowshowers_day", "❄️")]
    [InlineData("sleet", "🌨️")]
    [InlineData("fog", "🌫️")]
    [InlineData("something_unknown", "🌡️")]
    public void MapSymbolToEmoji_KnownCodes(string code, string expected)
    {
        Assert.Equal(expected, ForecastMapper.MapSymbolToEmoji(code));
    }

    [Theory]
    [InlineData("clearsky_day", "Klar")]
    [InlineData("fair_night", "Heiter")]
    [InlineData("partlycloudy_day", "Teils bewölkt")]
    [InlineData("heavyrainshowers_night", "Regen")]
    [InlineData("heavyrainandthunder", "Gewitter")]
    [InlineData("lightsnow", "Schnee")]
    [InlineData("fog", "Nebel")]
    [InlineData("???", "Wetter")]
    public void MapSymbolToDescription_KnownCodes(string code, string expected)
    {
        Assert.Equal(expected, ForecastMapper.MapSymbolToDescription(code));
    }

    [Fact]
    public void ToLocal_UnspecifiedKind_TreatedAsUtc()
    {
        var unspecified = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Unspecified);
        var expected = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
        Assert.Equal(expected, ForecastMapper.ToLocal(unspecified));
    }

    [Fact]
    public void ToLocal_LocalKind_Unchanged()
    {
        var local = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Local);
        Assert.Equal(local, ForecastMapper.ToLocal(local));
    }

    [Fact]
    public void GroupByDay_CapsAtSevenDays()
    {
        var forecast = new MetForecastResponse();
        var start = DateTime.UtcNow.Date;
        for (int d = 0; d < 10; d++)
            for (int h = 0; h < 3; h++)
                forecast.Properties.Timeseries.Add(MakeItem(start.AddDays(d).AddHours(h)));

        var groups = ForecastMapper.GroupByDay(forecast);

        Assert.Equal(7, groups.Count);
        Assert.True(groups.Select(g => g.Date).SequenceEqual(groups.Select(g => g.Date).OrderBy(x => x)));
    }

    [Fact]
    public void GroupByDay_EmptyTimeseries_ReturnsEmpty()
    {
        var groups = ForecastMapper.GroupByDay(new MetForecastResponse());
        Assert.Empty(groups);
    }

    [Fact]
    public void BuildDayGroup_Today_LabelAndRange()
    {
        var today = DateTime.Now.Date;
        var items = new List<TimeSeriesItem>
        {
            MakeItem(ForecastMapper.ToLocal(DateTime.UtcNow).Date.AddHours(6), temp: 10.2, symbol: "partlycloudy_day"),
            MakeItem(ForecastMapper.ToLocal(DateTime.UtcNow).Date.AddHours(12), temp: 18.7, symbol: "partlycloudy_day"),
            MakeItem(ForecastMapper.ToLocal(DateTime.UtcNow).Date.AddHours(18), temp: 14.1, symbol: "fair_night"),
        };

        var group = ForecastMapper.BuildDayGroup(today, items);

        Assert.StartsWith("Heute", group.DayLabel);
        Assert.Equal("10 – 19 °C", group.TempRangeDisplay);
        Assert.Equal(3, group.Cards.Count);
        Assert.Single(group.TemperatureSeries);
        Assert.Equal(3, group.XAxes[0].Labels!.Count);
        Assert.Equal("Teils bewölkt", group.SummaryDisplay); // dominant symbol
        Assert.Equal("🌤️", group.Cards[2].IconEmoji);
    }

    [Fact]
    public void SelectNextHours_EmptyForecast_ReturnsEmpty()
    {
        Assert.Empty(ForecastMapper.SelectNextHours(new MetForecastResponse()));
    }

    [Fact]
    public void SelectNextHours_TakesMaxFour()
    {
        var forecast = new MetForecastResponse();
        var now = DateTime.UtcNow;
        for (int h = 0; h < 10; h++)
            forecast.Properties.Timeseries.Add(MakeItem(now.AddHours(h)));

        Assert.Equal(4, ForecastMapper.SelectNextHours(forecast).Count);
    }

    [Fact]
    public void BuildHourCards_MapsAllFields()
    {
        var cards = ForecastMapper.BuildHourCards(new List<TimeSeriesItem>
        {
            MakeItem(DateTime.UtcNow, temp: 15.5, humidity: 70, wind: 3.2, precip: 0.4, symbol: "lightrainshowers_day")
        });

        var card = Assert.Single(cards);
        Assert.Equal("Regen", card.Description);
        Assert.Equal("🌧️", card.IconEmoji);
        Assert.Equal(15.5.ToString("F1") + " °C", card.TempDisplay); // Locale-abhängig (de: "15,5")
        Assert.Equal(0.4, card.PrecipitationAmount);
    }

    [Theory]
    [InlineData(new double[] { 0.0, 0.0 }, 1.0)]
    [InlineData(new double[] { 0.2, 0.5 }, 1.0)]
    [InlineData(new double[] { 2.3 }, 3.0)]
    public void BuildPrecipitationData_YMaxLimit(double[] amounts, double expectedMax)
    {
        var items = amounts.Select((a, i) => MakeItem(DateTime.UtcNow.AddHours(i), precip: a)).ToList();

        var (series, xAxes, yMax) = ForecastMapper.BuildPrecipitationData(items);

        Assert.Single(series);
        Assert.Equal(amounts.Length, xAxes[0].Labels!.Count);
        Assert.Equal(expectedMax, yMax);
    }

    private static TimeSeriesItem MakeItem(
        DateTime time,
        double temp = 15.0,
        double humidity = 60,
        double wind = 2.0,
        double precip = 0.0,
        string symbol = "fair_day")
    {
        return new TimeSeriesItem
        {
            Time = time,
            Data = new TimeSeriesData
            {
                Instant = new InstantData
                {
                    Details = new InstantDetails
                    {
                        AirTemperature = temp,
                        RelativeHumidity = humidity,
                        WindSpeed = wind,
                        WindFromDirection = 180,
                    }
                },
                Next1Hours = new PeriodSummary
                {
                    Summary = new SymbolSummary { SymbolCode = symbol },
                    Details = new PeriodDetails { PrecipitationAmount = precip },
                },
            }
        };
    }
}
