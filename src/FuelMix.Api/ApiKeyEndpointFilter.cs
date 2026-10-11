using System.Security.Cryptography;
using System.Text;

namespace FuelMix.Api;

/// <summary>
/// Authentification par clé API (en-tête X-Api-Key), comparaison à temps constant.
/// La clé est configurée par environnement (Auth:ApiKey), jamais codée en dur.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private const string HeaderName = "X-Api-Key";

    private readonly byte[] _expected;

    public ApiKeyEndpointFilter(IConfiguration configuration)
    {
        var key = configuration["Auth:ApiKey"]
            ?? throw new InvalidOperationException("Auth:ApiKey is required.");

        _expected = Encoding.UTF8.GetBytes(key);
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!IsValid(provided))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }

    private bool IsValid(string provided)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        if (providedBytes.Length != _expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, _expected);
    }
}
