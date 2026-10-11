using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace FuelMix.Infrastructure.Data;

/// <summary>
/// Configuration Npgsql partagée. La table d'historique des migrations doit être
/// placée dans le schéma `fuelmix` (détenu par le rôle de migration) : le rôle
/// `fuelmix_migrator` n'a pas les droits sur `public`.
/// </summary>
public static class NpgsqlDefaults
{
    public const string Schema = "fuelmix";

    public static void Configure(NpgsqlDbContextOptionsBuilder options)
    {
        options.MigrationsHistoryTable("__EFMigrationsHistory", Schema);
    }
}
