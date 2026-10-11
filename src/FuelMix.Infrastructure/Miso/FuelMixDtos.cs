using System.Text.Json.Serialization;

namespace FuelMix.Infrastructure.Miso;

public class FuelMixResponse
{
    [JsonPropertyName("RefId")]
    public string? RefId { get; set; }

    [JsonPropertyName("TotalMW")]
    public string? TotalMw { get; set; }

    [JsonPropertyName("Fuel")]
    public FuelTypeContainer? Fuel { get; set; }
}

public class FuelTypeContainer
{
    [JsonPropertyName("Type")]
    public List<FuelTypeRow>? Type { get; set; }
}

public class FuelTypeRow
{
    /// <summary>Heure murale Eastern, format 12h (ex: "2026-10-11 10:35:00 AM").</summary>
    [JsonPropertyName("INTERVALEST")]
    public string? IntervalEst { get; set; }

    [JsonPropertyName("CATEGORY")]
    public string? Category { get; set; }

    /// <summary>Puissance en MW (chaîne, peut être négative).</summary>
    [JsonPropertyName("ACT")]
    public string? Act { get; set; }

    /// <summary>Libellé formaté redondant (ex: "Coal  (15,844 MW)") — non stocké.</summary>
    [JsonPropertyName("FUEL_CATEGORY")]
    public string? FuelCategory { get; set; }
}
