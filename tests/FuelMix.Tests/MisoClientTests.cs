using System.Net;
using System.Text;
using FuelMix.Infrastructure.Miso;

namespace FuelMix.Tests;

public class MisoClientTests
{
    [Fact]
    public async Task GetFuelMix_returns_parsed_rows_and_uses_expected_path()
    {
        const string json = """
            {
              "RefId": "11-Oct-2026 - Interval 10:35 EST",
              "TotalMW": "70558",
              "Fuel": {
                "Type": [
                  { "INTERVALEST": "2026-10-11 10:35:00 AM", "CATEGORY": "Coal", "ACT": "15844", "FUEL_CATEGORY": "Coal  (15,844 MW)" },
                  { "INTERVALEST": "2026-10-11 10:35:00 AM", "CATEGORY": "Wind", "ACT": "10403", "FUEL_CATEGORY": "Wind  (10,403 MW)" }
                ]
              }
            }
            """;
        var handler = new StubHandler(json);
        var client = new MisoClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        var rows = await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Coal", rows[0].Category);
        Assert.Equal("15844", rows[0].Act);
        Assert.Equal("Wind", rows[1].Category);
        Assert.Equal("/api/FuelMix", handler.LastUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(FuelMixSource.Today, "/api/FuelMix/Today")]
    [InlineData(FuelMixSource.Yesterday, "/api/FuelMix/Yesterday")]
    public async Task GetFuelMix_uses_correct_path_per_source(FuelMixSource source, string expectedPath)
    {
        var handler = new StubHandler("""{"RefId":"x","Fuel":{"Type":[]}}""");
        var client = new MisoClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        await client.GetFuelMixAsync(source);

        Assert.Equal(expectedPath, handler.LastUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetFuelMix_returns_empty_when_fuel_type_is_missing()
    {
        var handler = new StubHandler("""{"RefId":"x"}""");
        var client = new MisoClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        var rows = await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Empty(rows);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
