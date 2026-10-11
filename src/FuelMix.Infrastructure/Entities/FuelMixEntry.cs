namespace FuelMix.Infrastructure.Entities;

/// <summary>
/// Une mesure de mix énergétique pour un intervalle de 5 minutes et une catégorie.
/// Clé métier : (interval_utc, category). Les valeurs négatives (Battery Storage, Imports) sont autorisées.
/// </summary>
public class FuelMixEntry
{
    public DateTimeOffset IntervalUtc { get; set; }

    public string Category { get; set; } = default!;

    public decimal Megawatts { get; set; }

    /// <summary>Dernière modification réelle de la valeur (mise à jour seulement si Megawatts change).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
