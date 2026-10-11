using FuelMix.Infrastructure.Miso;

namespace FuelMix.Tests;

public class MisoDateConverterTests
{
    [Fact]
    public void Summer_midnight_converts_to_04_utc()
    {
        var result = MisoDateConverter.ToUtc("2026-07-01 12:00:00 AM");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset); // normalisé en UTC
        Assert.Equal(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc), result.Value.UtcDateTime);
    }

    [Fact]
    public void Winter_midnight_converts_to_05_utc()
    {
        var result = MisoDateConverter.ToUtc("2026-01-15 12:00:00 AM");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset);
        Assert.Equal(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc), result.Value.UtcDateTime);
    }

    [Fact]
    public void Same_wall_clock_time_maps_to_different_utc_between_seasons_no_fixed_offset()
    {
        var summer = MisoDateConverter.ToUtc("2026-07-01 12:00:00 AM")!.Value;
        var winter = MisoDateConverter.ToUtc("2026-01-15 12:00:00 AM")!.Value;

        // Même heure murale (00:00) : EDT => 04:00 UTC, EST => 05:00 UTC (pas de décalage fixe).
        Assert.Equal(4, summer.UtcDateTime.Hour);
        Assert.Equal(5, winter.UtcDateTime.Hour);
    }

    [Fact]
    public void Ambiguous_time_fall_back_is_rejected()
    {
        // 2026-11-01 01:30 AM se produit deux fois (EDT puis EST).
        Assert.Null(MisoDateConverter.ToUtc("2026-11-01 01:30:00 AM"));
    }

    [Fact]
    public void Invalid_time_spring_forward_is_rejected()
    {
        // 2026-03-08 02:30 AM n'existe pas (passage 02:00 -> 03:00 EDT).
        Assert.Null(MisoDateConverter.ToUtc("2026-03-08 02:30:00 AM"));
    }

    [Fact]
    public void Single_digit_hour_is_parsed()
    {
        // MISO émet les heures 1-9 sans zéro initial.
        var result = MisoDateConverter.ToUtc("2026-07-01 9:55:00 AM");

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 7, 1, 13, 55, 0, DateTimeKind.Utc), result.Value.UtcDateTime);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-date")]
    public void Malformed_input_is_rejected(string? input)
    {
        Assert.Null(MisoDateConverter.ToUtc(input));
    }
}
