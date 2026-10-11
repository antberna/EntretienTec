using System.Globalization;

namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Convertit une chaîne INTERVALEST MISO (heure murale America/New_York, format 12h)
/// en instant UTC. La conversion est DST-aware (aucun décalage fixe) :
///  - heure inexistante (gap du passage à l'heure d'été)  -> null (ignorée + loguée) ;
///  - heure ambiguë (chevauchenent du retour à l'heure standard) -> null (ignorée + loguée).
/// La source ne fournissant pas d'offset, on refuse de deviner : on préfère ignorer
/// une mesure ambiguë plutôt que de prétendre garantir son exactitude.
/// </summary>
public static class MisoDateConverter
{
    private const string TimeZoneId = "America/New_York";
    private const string Format = "yyyy-MM-dd hh:mm:ss tt";

    private static readonly TimeZoneInfo Eastern =
        TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateTimeOffset? ToUtc(string? intervalEst)
    {
        if (string.IsNullOrWhiteSpace(intervalEst))
        {
            return null;
        }

        if (!DateTime.TryParseExact(intervalEst, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var naive))
        {
            return null;
        }

        if (Eastern.IsInvalidTime(naive) || Eastern.IsAmbiguousTime(naive))
        {
            return null;
        }

        return new DateTimeOffset(naive, Eastern.GetUtcOffset(naive));
    }
}
