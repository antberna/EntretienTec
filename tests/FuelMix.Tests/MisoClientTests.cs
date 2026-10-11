using System.Net;
using System.Text;
using FuelMix.Infrastructure.Miso;

namespace FuelMix.Tests;

public class MisoClientTests
{
    private const string EmptyJson = """{"RefId":"x","Fuel":{"Type":[]}}""";

    // --- chemin / mapping (comportement de base) ---

    [Fact]
    public async Task GetFuelMix_returns_parsed_rows_and_uses_expected_path()
    {
        const string json = """
            {
              "RefId": "11-Oct-2026 - Interval 10:35 EST",
              "Fuel": { "Type": [ { "INTERVALEST": "2026-10-11 10:35:00 AM", "CATEGORY": "Coal", "ACT": "15844" } ] }
            }
            """;
        var handler = new StubHandler(_ => Ok(json));
        var client = CreateClient(handler).Client;

        var rows = await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Single(rows);
        Assert.Equal("Coal", rows[0].Category);
        Assert.Equal("/api/FuelMix", handler.LastUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(FuelMixSource.Today, "/api/FuelMix/Today")]
    [InlineData(FuelMixSource.Yesterday, "/api/FuelMix/Yesterday")]
    public async Task GetFuelMix_uses_correct_path_per_source(FuelMixSource source, string expectedPath)
    {
        var handler = new StubHandler(_ => Ok(EmptyJson));
        var client = CreateClient(handler).Client;

        await client.GetFuelMixAsync(source);

        Assert.Equal(expectedPath, handler.LastUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetFuelMix_returns_empty_when_fuel_type_is_missing()
    {
        var handler = new StubHandler(_ => Ok("""{"RefId":"x"}"""));
        var client = CreateClient(handler).Client;

        var rows = await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Empty(rows);
    }

    // --- résilience / retry ---

    [Fact]
    public async Task Retries_on_429_then_succeeds()
    {
        var handler = new StubHandler(n => n == 1 ? TooManyRequests() : Ok(EmptyJson));
        var (client, delays) = CreateClient(handler);

        var rows = await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Empty(rows);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(delays);
    }

    [Fact]
    public async Task Retries_on_500_then_succeeds()
    {
        var handler = new StubHandler(n => n == 1 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(EmptyJson));
        var (client, delays) = CreateClient(handler);

        await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Equal(2, handler.CallCount);
        Assert.Single(delays);
    }

    [Fact]
    public async Task Fails_immediately_on_404_without_retry()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (client, delays) = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<MisoClientException>(() => client.GetFuelMixAsync(FuelMixSource.Live));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task Exhausted_attempts_throws_and_backs_off_exponentially()
    {
        var handler = new StubHandler(_ => TooManyRequests());
        var (client, delays) = CreateClient(handler); // MaxAttempts=3, base 1s, jitter 0

        await Assert.ThrowsAsync<MisoClientException>(() => client.GetFuelMixAsync(FuelMixSource.Live));

        Assert.Equal(3, handler.CallCount);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, delays);
    }

    [Fact]
    public async Task Retry_after_seconds_is_respected_exactly()
    {
        var handler = new StubHandler(n => n == 1 ? TooManyRequests("5") : Ok(EmptyJson));
        var (client, delays) = CreateClient(handler);

        await client.GetFuelMixAsync(FuelMixSource.Live);

        Assert.Equal(TimeSpan.FromSeconds(5), Assert.Single(delays));
    }

    [Fact]
    public async Task Retry_after_http_date_is_respected()
    {
        var date = DateTimeOffset.UtcNow.AddSeconds(5).ToString("R"); // RFC 1123
        var handler = new StubHandler(n => n == 1 ? TooManyRequests(date) : Ok(EmptyJson));
        var (client, delays) = CreateClient(handler);

        await client.GetFuelMixAsync(FuelMixSource.Live);

        var delay = Assert.Single(delays);
        Assert.InRange(delay, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task Cancellation_during_wait_is_propagated_without_further_calls()
    {
        var handler = new StubHandler(_ => TooManyRequests());
        var client = new MisoClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new MisoClientOptions(),
            delay: (_, _) => throw new OperationCanceledException(),
            jitter: () => 0);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetFuelMixAsync(FuelMixSource.Live));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Retry_after_exceeding_budget_is_abandoned()
    {
        var handler = new StubHandler(_ => TooManyRequests("9999"));
        var client = new MisoClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new MisoClientOptions { ExecutionBudgetSeconds = 0 },
            delay: (_, _) => Task.CompletedTask,
            jitter: () => 0);

        await Assert.ThrowsAsync<MisoClientException>(() => client.GetFuelMixAsync(FuelMixSource.Live));

        Assert.Equal(1, handler.CallCount);
    }

    // --- helpers ---

    private static (MisoClient Client, List<TimeSpan> Delays) CreateClient(StubHandler handler)
    {
        var delays = new List<TimeSpan>();
        var client = new MisoClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new MisoClientOptions(), // 3 tentatives, base 1s
            delay: (d, _) => { delays.Add(d); return Task.CompletedTask; },
            jitter: () => 0);        // déterministe
        return (client, delays);
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage TooManyRequests(string? retryAfter = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (retryAfter is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        }
        return response;
    }

    private sealed class StubHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastUri = request.RequestUri;
            return Task.FromResult(respond(CallCount));
        }
    }
}
