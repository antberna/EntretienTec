using System.Net;

namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Erreur levée par le client MISO quand une requête échoue définitivement
/// (statut 4xx non récupérable, tentatives épuisées, budget d'exécution dépassé).
/// </summary>
public class MisoClientException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public MisoClientException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
