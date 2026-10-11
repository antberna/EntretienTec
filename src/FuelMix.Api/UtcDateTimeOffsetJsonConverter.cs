using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuelMix.Api;

/// <summary>
/// Sérialise les DateTimeOffset en ISO 8601 UTC avec suffixe « Z » (fractions
/// tronquées si nulles). Garantit une exposition des dates en UTC, quel que soit
/// l'offset d'origine.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
}
