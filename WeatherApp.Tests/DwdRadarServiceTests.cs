using System;
using WeatherApp.Services;

namespace WeatherApp.Tests;

public class DwdRadarServiceTests
{
    [Fact]
    public void BuildMapUrl_BboxAxisOrderAndFormat()
    {
        var time = new DateTime(2026, 9, 3, 12, 5, 0, DateTimeKind.Utc);

        string url = DwdRadarService.BuildMapUrl(51, 8, time, deltaLat: 1.0, deltaLon: 2.0);

        // WMS 1.3.0 + EPSG:4326: BBOX = latMin,lonMin,latMax,lonMax
        Assert.Contains("BBOX=50.00000,6.00000,52.00000,10.00000", url);
        Assert.Contains("TIME=2026-09-03T12%3A05%3A00.000Z", url);
        Assert.Contains("LAYERS=dwd:Niederschlagsradar", url);
        Assert.Contains("CRS=EPSG:4326", url);
        Assert.Contains("WIDTH=512&HEIGHT=512", url);
    }

    [Fact]
    public void BuildCurrentMapUrl_UsesCurrentTime()
    {
        string url = DwdRadarService.BuildCurrentMapUrl(51, 8);

        Assert.Contains("TIME=current", url);
        Assert.Contains("BBOX=", url);
    }

    [Fact]
    public void GetDeltaLatForKm_150km()
    {
        Assert.Equal(150.0 / 111.0, DwdRadarService.GetDeltaLatForKm(150), precision: 10);
    }

    [Fact]
    public void GetDeltaLonForLat_GrowsWithLatitude()
    {
        double equator = DwdRadarService.GetDeltaLonForLat(0, 150);
        double sauerland = DwdRadarService.GetDeltaLonForLat(51.36, 150);

        Assert.Equal(150.0 / 111.32, equator, precision: 6);
        Assert.InRange(sauerland, 2.1, 2.2);
        Assert.True(sauerland > equator);
    }
}
