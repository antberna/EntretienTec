using System.Globalization;
using FuelMix.Infrastructure.Entities;

namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Transforme une ligne MISO brute en entité persistable, ou signale un motif
/// d'exclusion (date ambiguë/inexistante, catégorie manquante, ACT non numérique).
/// </summary>
public static class MisoMapper
{
    public static bool TryMap(FuelTypeRow row, out FuelMixEntry? entry, out string? skipReason)
    {
        entry = null;
        skipReason = null;

        var utc = MisoDateConverter.ToUtc(row.IntervalEst);
        if (utc is null)
        {
            skipReason = $"interval '{(row.IntervalEst ?? "<null>")}' is unparseable, invalid or ambiguous (DST)";
            return false;
        }

        if (string.IsNullOrWhiteSpace(row.Category))
        {
            skipReason = "missing category";
            return false;
        }

        if (!decimal.TryParse(row.Act, NumberStyles.Number, CultureInfo.InvariantCulture, out var megawatts))
        {
            skipReason = $"ACT is not numeric: '{row.Act}'";
            return false;
        }

        entry = new FuelMixEntry
        {
            IntervalUtc = utc.Value,
            Category = row.Category,
            Megawatts = megawatts
        };

        return true;
    }
}
