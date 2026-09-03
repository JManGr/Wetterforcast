using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WeatherApp.Services;

namespace WeatherApp.Tests;

public class WeatherServiceTests
{
    private const string MinimalForecastJson = """
        {"properties": {"timeseries": [
          {"time": "2026-09-03T12:00:00Z",
           "data": {"instant": {"details": {
               "air_temperature": 15.5, "relative_humidity": 70,
               "wind_speed": 3.2, "wind_from_direction": 180}},
             "next_1_hours": {"summary": {"symbol_code": "partlycloudy_day"},
               "details": {"precipitation_amount": 0.2}}}}
        ]}}
        """;

    [Fact]
    public async Task GetForecastAsync_ParsesResponse()
    {
        using var service = new WeatherService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, MinimalForecastJson)));

        var forecast = await service.GetForecastAsync(51.36, 8.39);

        Assert.NotNull(forecast);
        var item = Assert.Single(forecast.Properties.Timeseries);
        Assert.Equal(15.5, item.Data.Instant.Details.AirTemperature);
        Assert.Equal("partlycloudy_day", item.Data.Next1Hours!.Summary.SymbolCode);
        Assert.Equal(0.2, item.Data.Next1Hours.Details.PrecipitationAmount);
        Assert.Equal(DateTimeKind.Utc, item.Time.Kind);
    }

    [Fact]
    public async Task GetForecastAsync_Forbidden_ThrowsWithStatus()
    {
        using var service = new WeatherService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden, "blocked")));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetForecastAsync(51.0, 8.0));

        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public async Task GetForecastAsync_SendsInvariantCoordinates()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, MinimalForecastJson);
        using var service = new WeatherService(new HttpClient(handler));

        await service.GetForecastAsync(51.36, 8.39);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("lat=51.36", handler.LastRequest.RequestUri!.Query);
        Assert.Contains("lon=8.39", handler.LastRequest.RequestUri.Query);
    }
}
