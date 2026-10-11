using FuelMix.Infrastructure.Miso;

namespace FuelMix.Tests;

public class MisoMapperTests
{
    [Fact]
    public void Valid_row_maps_to_entry()
    {
        var row = new FuelTypeRow
        {
            IntervalEst = "2026-07-01 12:00:00 AM",
            Category = "Coal",
            Act = "15844",
            FuelCategory = "Coal  (15,844 MW)" // ignoré
        };

        var ok = MisoMapper.TryMap(row, out var entry, out var reason);

        Assert.True(ok);
        Assert.Null(reason);
        Assert.NotNull(entry);
        Assert.Equal("Coal", entry!.Category);
        Assert.Equal(15844m, entry.Megawatts);
        Assert.Equal(new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc), entry.IntervalUtc.UtcDateTime);
    }

    [Fact]
    public void Negative_megawatts_are_preserved()
    {
        var row = new FuelTypeRow { IntervalEst = "2026-07-01 12:00:00 AM", Category = "Battery Storage", Act = "-632" };

        var ok = MisoMapper.TryMap(row, out var entry, out _);

        Assert.True(ok);
        Assert.Equal(-632m, entry!.Megawatts);
    }

    [Fact]
    public void Ambiguous_interval_is_skipped_with_reason()
    {
        var row = new FuelTypeRow { IntervalEst = "2026-11-01 01:30:00 AM", Category = "Coal", Act = "100" };

        var ok = MisoMapper.TryMap(row, out var entry, out var reason);

        Assert.False(ok);
        Assert.Null(entry);
        Assert.Contains("ambiguous", reason);
    }

    [Fact]
    public void Non_numeric_act_is_skipped()
    {
        var row = new FuelTypeRow { IntervalEst = "2026-07-01 12:00:00 AM", Category = "Coal", Act = "abc" };

        var ok = MisoMapper.TryMap(row, out _, out var reason);

        Assert.False(ok);
        Assert.Contains("numeric", reason);
    }

    [Fact]
    public void Missing_category_is_skipped()
    {
        var row = new FuelTypeRow { IntervalEst = "2026-07-01 12:00:00 AM", Category = null, Act = "100" };

        var ok = MisoMapper.TryMap(row, out _, out var reason);

        Assert.False(ok);
        Assert.Contains("category", reason);
    }
}
