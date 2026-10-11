using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FuelMix.Infrastructure.Data;

/// <summary>
/// Fabrique utilisée par `dotnet ef` (design-time) pour générer les migrations.
/// La chaîne de connexion provient de la variable d'environnement
/// FUELMIX_MIGRATOR_CONNECTION, avec une valeur de repli pour le développement local.
/// </summary>
public class FuelMixDesignTimeFactory : IDesignTimeDbContextFactory<FuelMixDbContext>
{
    public FuelMixDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FUELMIX_MIGRATOR_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=fuelmix;Username=fuelmix_migrator;Password=migrator";

        var options = new DbContextOptionsBuilder<FuelMixDbContext>()
            .UseNpgsql(connectionString, NpgsqlDefaults.Configure)
            .Options;

        return new FuelMixDbContext(options);
    }
}
