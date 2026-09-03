using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WeatherApp.Services;

namespace WeatherApp.Tests;

public class GeocodingServiceTests
{
    [Fact]
    public async Task ResolveCandidatesAsync_TwoResults_ReturnsBoth()
    {
        const string json = """
            {"results": [
              {"id": 1, "name": "Frankfurt", "latitude": 50.11, "longitude": 8.68,
               "country": "Deutschland", "admin1": "Hessen", "elevation": 112},
              {"id": 2, "name": "Frankfurt", "latitude": 52.35, "longitude": 14.55,
               "country": "Deutschland", "admin1": "Brandenburg", "elevation": 19}
            ]}
            """;
        using var service = new GeocodingService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        var candidates = await service.ResolveCandidatesAsync("Frankfurt");

        Assert.Equal(2, candidates.Count);
        Assert.Equal(50.11, candidates[0].Latitude);
        Assert.Equal("Frankfurt, Hessen, Deutschland", candidates[0].DisplayName);
        Assert.Equal("Frankfurt, Brandenburg, Deutschland", candidates[1].DisplayName);
    }

    [Fact]
    public async Task ResolveAsync_TwoResults_ReturnsFirst()
    {
        const string json = """
            {"results": [
              {"id": 1, "name": "A", "latitude": 1.0, "longitude": 2.0,
               "country": "C", "admin1": "R", "elevation": 0},
              {"id": 2, "name": "B", "latitude": 3.0, "longitude": 4.0,
               "country": "C", "admin1": "", "elevation": 0}
            ]}
            """;
        using var service = new GeocodingService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        var result = await service.ResolveAsync("x");

        Assert.NotNull(result);
        Assert.Equal("A", result.Name);
    }

    [Theory]
    [InlineData("""{"results": []}""")]
    [InlineData("""{}""")]
    [InlineData("""{"results": null}""")]
    public async Task ResolveCandidatesAsync_NoResults_ReturnsEmpty(string json)
    {
        using var service = new GeocodingService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        Assert.Empty(await service.ResolveCandidatesAsync("Nirgendwo"));
        Assert.Null(await service.ResolveAsync("Nirgendwo"));
    }

    [Fact]
    public async Task ResolveCandidatesAsync_EmptyCity_Throws()
    {
        using var service = new GeocodingService(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}")));

        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveCandidatesAsync("  "));
    }

    [Fact]
    public void DisplayName_WithoutAdmin1_OmitsEmptyPart()
    {
        var r = new Models.GeocodingResult { Name = "Bestwig", Country = "Deutschland", Admin1 = "" };
        Assert.Equal("Bestwig, Deutschland", r.DisplayName);
    }
}
