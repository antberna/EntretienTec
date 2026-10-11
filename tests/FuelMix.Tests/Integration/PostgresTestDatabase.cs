using FuelMix.Infrastructure.Data;
using Npgsql;

namespace FuelMix.Tests.Integration;

/// <summary>
/// Fournit une base PostgreSQL dédiée `fuelmix_test` pour les tests d'intégration.
/// Recrée la base, les rôles (si absents), le schéma `fuelmix` et applique les
/// migrations au début du cycle de tests (fixture de collection xUnit).
/// Connexions configurables via les variables FUELMIX_TEST_* (repli local).
/// </summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
    public string MigratorConnection { get; }
    public string IngestConnection { get; }
    public string ReportConnection { get; }

    private readonly string _adminConnection;
    private readonly string _adminToTestDb;
    private readonly string _databaseName;

    private const string RolesSql = """
        DO $$
        BEGIN
          IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'fuelmix_migrator') THEN
            CREATE ROLE fuelmix_migrator LOGIN PASSWORD 'migrator';
          END IF;
          IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'fuelmix_ingest') THEN
            CREATE ROLE fuelmix_ingest LOGIN PASSWORD 'ingest';
          END IF;
          IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'fuelmix_report') THEN
            CREATE ROLE fuelmix_report LOGIN PASSWORD 'report';
          END IF;
        END $$;
        """;

    private const string SchemaSql = """
        CREATE SCHEMA fuelmix AUTHORIZATION fuelmix_migrator;
        GRANT USAGE ON SCHEMA fuelmix TO fuelmix_ingest, fuelmix_report;
        ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
            GRANT SELECT ON TABLES TO fuelmix_report;
        ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
            GRANT SELECT, INSERT, UPDATE ON TABLES TO fuelmix_ingest;
        ALTER DEFAULT PRIVILEGES FOR ROLE fuelmix_migrator IN SCHEMA fuelmix
            GRANT USAGE, SELECT ON SEQUENCES TO fuelmix_ingest;
        """;

    public PostgresTestDatabase()
    {
        var host = Environment.GetEnvironmentVariable("FUELMIX_TEST_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("FUELMIX_TEST_PORT") ?? "5432";
        var adminUser = Environment.GetEnvironmentVariable("FUELMIX_TEST_ADMIN_USER") ?? "postgres";
        var adminPassword = Environment.GetEnvironmentVariable("FUELMIX_TEST_ADMIN_PASSWORD") ?? "postgres";
        _databaseName = Environment.GetEnvironmentVariable("FUELMIX_TEST_DB") ?? "fuelmix_test";

        _adminConnection = $"Host={host};Port={port};Username={adminUser};Password={adminPassword};Database=postgres";
        _adminToTestDb = $"Host={host};Port={port};Username={adminUser};Password={adminPassword};Database={_databaseName}";
        MigratorConnection = $"Host={host};Port={port};Database={_databaseName};Username=fuelmix_migrator;Password=migrator";
        IngestConnection = $"Host={host};Port={port};Database={_databaseName};Username=fuelmix_ingest;Password=ingest";
        ReportConnection = $"Host={host};Port={port};Database={_databaseName};Username=fuelmix_report;Password=report";
    }

    public async Task InitializeAsync()
    {
        await RecreateDatabaseAsync();
        await CreateSchemaAsync();
        DatabaseMigrator.Migrate(MigratorConnection);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task RecreateDatabaseAsync()
    {
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();

        await using (var roles = new NpgsqlCommand(RolesSql, admin))
        {
            await roles.ExecuteNonQueryAsync();
        }

        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);", admin))
        {
            await drop.ExecuteNonQueryAsync();
        }

        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\";", admin))
        {
            await create.ExecuteNonQueryAsync();
        }
    }

    private async Task CreateSchemaAsync()
    {
        await using var conn = new NpgsqlConnection(_adminToTestDb);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(SchemaSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
