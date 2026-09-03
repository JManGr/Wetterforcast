using WeatherApp.Services;

namespace WeatherApp.Tests;

public class OrientationServiceTests
{
    [Fact]
    public void HaversineKm_SamePoint_Zero()
    {
        Assert.Equal(0, OrientationService.HaversineKm(51.3, 8.4, 51.3, 8.4));
    }

    [Fact]
    public void HaversineKm_BerlinHamburg_Plausible()
    {
        // Berlin (52.52, 13.405) – Hamburg (53.551, 9.994): ~255 km
        double dist = OrientationService.HaversineKm(52.5200, 13.4050, 53.5511, 9.9937);
        Assert.InRange(dist, 240, 270);
    }

    [Fact]
    public void HaversineKm_Symmetric()
    {
        double a = OrientationService.HaversineKm(51.0, 8.0, 48.0, 11.0);
        double b = OrientationService.HaversineKm(48.0, 11.0, 51.0, 8.0);
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildPins_Bestwig_ReturnsCappedPinsInBounds()
    {
        // Bestwig im Sauerland
        var (pins, status) = OrientationService.BuildPins(51.3630, 8.3945, "Bestwig");

        Assert.NotEmpty(pins);
        Assert.True(pins.Count <= 15);
        Assert.All(pins, p =>
        {
            Assert.InRange(p.DistanceKm, 5, 150);
            Assert.InRange(p.X, 12, 500);
            Assert.InRange(p.Y, 12, 500);
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
        });
        Assert.Contains("Labels sichtbar", status);
        Assert.Contains("Bestwig", status);
    }

    [Fact]
    public void BuildPins_MidOcean_ReturnsEmpty()
    {
        var (pins, status) = OrientationService.BuildPins(0, 0, "Atlantik");

        Assert.Empty(pins);
        Assert.Equal("Keine Orte 150km gefunden", status);
    }

    [Fact]
    public void BuildPins_SortedByDistance()
    {
        var (pins, _) = OrientationService.BuildPins(51.3630, 8.3945, "Bestwig");

        var distances = pins.Select(p => p.DistanceKm).ToList();
        Assert.True(distances.SequenceEqual(distances.OrderBy(x => x)));
    }
}
