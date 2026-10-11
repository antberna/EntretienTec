using FuelMix.Infrastructure.Entities;
using FuelMix.Infrastructure.Ingestion;
using Npgsql;

namespace FuelMix.Tests.Integration;

[Collection("Postgres")]
public class FuelMixUpserterIntegrationTests
{
    private readonly FuelMixUpserter _upserter;
    private readonly string _reportConnection;

    public FuelMixUpserterIntegrationTests(PostgresTestDatabase db)
    {
        _upserter = new FuelMixUpserter(db.IngestConnection);
        _reportConnection = db.ReportConnection;
    }

    [Fact]
    public async Task Upsert_is_idempotent_and_reports_exact_counters()
    {
        var interval = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = new[] { "Coal", "Wind", "Solar" }
            .Select(c => new FuelMixEntry { IntervalUtc = interval, Category = c, Megawatts = 100 })
            .ToList();

        var first = await _upserter.UpsertAsync(entries);
        Assert.Equal(3, first.Inserted);
        Assert.Equal(0, first.Updated);

        // Même données -> aucune écriture (idempotent), aucun doublon.
        var second = await _upserter.UpsertAsync(entries);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Updated);

        Assert.Equal(3, await CountRowsAsync(interval));

        // Valeur modifiée -> mise à jour réelle.
        var changed = new List<FuelMixEntry>
        {
            new() { IntervalUtc = interval, Category = "Coal", Megawatts = 150 }
        };
        var third = await _upserter.UpsertAsync(changed);
        Assert.Equal(0, third.Inserted);
        Assert.Equal(1, third.Updated);

        Assert.Equal(150m, await GetMegawattsAsync(interval, "Coal"));
    }

    [Fact]
    public async Task Failed_batch_rolls_back_all_rows()
    {
        var interval = new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.Zero);
        var entries = new List<FuelMixEntry>
        {
            new() { IntervalUtc = interval, Category = "Coal", Megawatts = 100 },
            // dépasse varchar(64) -> échec au milieu du lot
            new() { IntervalUtc = interval, Category = new string('x', 100), Megawatts = 200 }
        };

        await Assert.ThrowsAsync<PostgresException>(() => _upserter.UpsertAsync(entries));

        // La ligne valide a été annulée (transaction) : aucune trace persistée.
        Assert.Equal(0, await CountRowsAsync(interval));
    }

    private async Task<int> CountRowsAsync(DateTimeOffset interval)
    {
        await using var conn = new NpgsqlConnection(_reportConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM fuelmix.fuel_mix WHERE interval_utc = $1", conn);
        cmd.Parameters.AddWithValue(interval);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<decimal> GetMegawattsAsync(DateTimeOffset interval, string category)
    {
        await using var conn = new NpgsqlConnection(_reportConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT megawatts FROM fuelmix.fuel_mix WHERE interval_utc = $1 AND category = $2", conn);
        cmd.Parameters.AddWithValue(interval);
        cmd.Parameters.AddWithValue(category);
        return (decimal)(await cmd.ExecuteScalarAsync())!;
    }
}
