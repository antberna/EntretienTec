using System.Net.Http.Json;

namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Client HTTP de l'API publique MISO. L'ingestion périodique utilise l'endpoint
/// /api/FuelMix ; les imports manuels peuvent cibler /Today et /Yesterday.
/// Le rythme d'appel (une requête par intervalle) reste largement sous le rate-limit.
/// </summary>
public class MisoClient
{
    private readonly HttpClient _http;

    public MisoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<FuelTypeRow>> GetFuelMixAsync(
        FuelMixSource source,
        CancellationToken cancellationToken = default)
    {
        var path = source switch
        {
            FuelMixSource.Live => "api/FuelMix",
            FuelMixSource.Today => "api/FuelMix/Today",
            FuelMixSource.Yesterday => "api/FuelMix/Yesterday",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        var response = await _http.GetFromJsonAsync<FuelMixResponse>(path, cancellationToken);
        return response?.Fuel?.Type ?? [];
    }
}
