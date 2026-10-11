using Npgsql;

namespace FuelMix.Tests.Integration;

[Collection("Postgres")]
public class RoleIsolationTests
{
    private readonly PostgresTestDatabase _db;

    public RoleIsolationTests(PostgresTestDatabase db)
    {
        _db = db;
    }

    [Fact]
    public async Task Report_role_can_select_but_not_insert()
    {
        // SELECT : autorisé (rôle de reporting).
        await using (var conn = new NpgsqlConnection(_db.ReportConnection))
        {
            await conn.OpenAsync();
            await using var select = new NpgsqlCommand("SELECT count(*) FROM fuelmix.fuel_mix", conn);
            await select.ExecuteScalarAsync();
        }

        // INSERT : refusé (lecture seule).
        await using var insertConn = new NpgsqlConnection(_db.ReportConnection);
        await insertConn.OpenAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO fuelmix.fuel_mix (interval_utc, category, megawatts, updated_at) VALUES (now(), 'Coal', 1, now())",
            insertConn);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege
    }

    [Fact]
    public async Task Ingest_role_cannot_create_tables()
    {
        await using var conn = new NpgsqlConnection(_db.IngestConnection);
        await conn.OpenAsync();
        await using var create = new NpgsqlCommand("CREATE TABLE fuelmix.should_fail (id int)", conn);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => create.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege (pas de DDL)
    }
}
