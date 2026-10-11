using Microsoft.EntityFrameworkCore;

namespace FuelMix.Infrastructure.Data;

/// <summary>
/// Applique les migrations avec le rôle `fuelmix_migrator` (DDL), distinct du rôle
/// d'ingestion. Utilisé au démarrage du Worker.
/// </summary>
public static class DatabaseMigrator
{
    public static void Migrate(string connectionString)
    {
        var options = new DbContextOptionsBuilder<FuelMixDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new FuelMixDbContext(options);
        context.Database.Migrate();
    }
}
