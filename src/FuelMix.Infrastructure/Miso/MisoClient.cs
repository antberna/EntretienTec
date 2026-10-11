using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Client HTTP de l'API publique MISO avec gestion de la résilience :
///  - retry sur 429/408/5xx et erreurs réseau transitoires ;
///  - échec immédiat sur 4xx non récupérables ;
///  - respect strict de l'en-tête Retry-After (secondes ou date HTTP), jamais raccourci ;
///  - sinon backoff exponentiel avec jitter ;
///  - nombre de tentatives, délai de base et budget d'exécution configurables.
/// Aucun retry n'est effectué par ailleurs dans le pipeline d'ingestion (pas de retry imbriqué).
/// </summary>
public class MisoClient
{
    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes = new()
    {
        HttpStatusCode.RequestTimeout,      // 408
        HttpStatusCode.TooManyRequests,     // 429
        HttpStatusCode.InternalServerError, // 500
        HttpStatusCode.BadGateway,          // 502
        HttpStatusCode.ServiceUnavailable,  // 503
        HttpStatusCode.GatewayTimeout       // 504
    };

    private readonly HttpClient _http;
    private readonly MisoClientOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double> _jitter;

    public MisoClient(
        HttpClient http,
        MisoClientOptions options,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<double>? jitter = null)
    {
        _http = http;
        _options = options;
        _delay = delay ?? Task.Delay;
        _jitter = jitter ?? (() => Random.Shared.NextDouble());
    }

    public async Task<IReadOnlyList<FuelTypeRow>> GetFuelMixAsync(
        FuelMixSource source,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(source);
        var deadline = DateTimeOffset.UtcNow + _options.ExecutionBudget;

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage? response;
            try
            {
                response = await _http.GetAsync(path, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                response = null; // timeout du client -> transitoire
            }
            catch (HttpRequestException)
            {
                response = null; // erreur réseau -> transitoire
            }

            HttpStatusCode? lastStatus = null;
            TimeSpan? retryAfter = null;

            using (response)
            {
                if (response is not null)
                {
                    lastStatus = response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadFromJsonAsync<FuelMixResponse>(cancellationToken);
                        return payload?.Fuel?.Type ?? [];
                    }

                    if (!RetryableStatusCodes.Contains(response.StatusCode))
                    {
                        throw new MisoClientException(
                            $"MISO returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                            response.StatusCode);
                    }

                    retryAfter = TryGetRetryAfter(response);
                }
            }

            if (attempt >= _options.MaxAttempts)
            {
                throw new MisoClientException($"MISO request failed after {attempt} attempt(s).", lastStatus);
            }

            var delay = retryAfter ?? ComputeBackoff(attempt);

            // On ne raccourcit jamais un Retry-After : si le délai dépasse le budget,
            // on abandonne et une exécution ultérieure reprendra le travail.
            if (DateTimeOffset.UtcNow + delay > deadline)
            {
                throw new MisoClientException(
                    $"Retry delay of {delay.TotalSeconds:F1}s exceeds the execution budget.",
                    HttpStatusCode.TooManyRequests);
            }

            await _delay(delay, cancellationToken);
        }
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var ms = _options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        ms *= 1 + _jitter() * 0.2;
        return TimeSpan.FromMilliseconds(ms);
    }

    private static TimeSpan? TryGetRetryAfter(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Forme « delta-seconds » : Retry-After: 120
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Forme « HTTP-date » : Retry-After: Wed, 21 Oct 2026 07:28:00 GMT
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            var delta = date - DateTimeOffset.UtcNow;
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }

    private static string ResolvePath(FuelMixSource source) => source switch
    {
        FuelMixSource.Live => "api/FuelMix",
        FuelMixSource.Today => "api/FuelMix/Today",
        FuelMixSource.Yesterday => "api/FuelMix/Yesterday",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
}
