using FuelMix.Infrastructure.Miso;

namespace FuelMix.Tests;

public class MisoDateConverterTests
{
    [Fact]
    public void Summer_date_converts_to_edt_offset()
    {
        var result = MisoDateConverter.ToUtc("2026-07-01 12:00:00 AM");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(-4), result.Value.Offset); // EDT
        Assert.Equal(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc), result.Value.UtcDateTime);
    }

    [Fact]
    public void Winter_date_converts_to_est_offset()
    {
        var result = MisoDateConverter.ToUtc("2026-01-15 12:00:00 AM");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(-5), result.Value.Offset); // EST
        Assert.Equal(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc), result.Value.UtcDateTime);
    }

    [Fact]
    public void Offsets_differ_between_summer_and_winter_no_fixed_offset()
    {
        var summer = MisoDateConverter.ToUtc("2026-07-01 12:00:00 AM")!.Value;
        var winter = MisoDateConverter.ToUtc("2026-01-15 12:00:00 AM")!.Value;

        Assert.NotEqual(summer.Offset, winter.Offset);
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

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-date")]
    public void Malformed_input_is_rejected(string? input)
    {
        Assert.Null(MisoDateConverter.ToUtc(input));
    }
}
