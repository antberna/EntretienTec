using FuelMix.Infrastructure.Entities;
using Npgsql;
using NpgsqlTypes;

namespace FuelMix.Infrastructure.Ingestion;

/// <summary>
/// Upsert PostgreSQL idempotent (clé composite `(interval_utc, category)`) exécuté
/// dans une transaction. Grâce à `RETURNING (xmax = 0)`, on distingue exactement :
///   - insertion (was_inserted = true)
///   - mise à jour réelle (was_inserted = false)
///   - aucune écriture (valeur inchangée -> la clause WHERE annule l'UPDATE, 0 ligne retournée)
/// </summary>
public class FuelMixUpserter
{
    private const string UpsertSql = """
        INSERT INTO fuelmix.fuel_mix (interval_utc, category, megawatts, updated_at)
        VALUES (@interval, @category, @megawatts, now())
        ON CONFLICT (interval_utc, category) DO UPDATE
            SET megawatts = EXCLUDED.megawatts,
                updated_at = now()
            WHERE fuelmix.fuel_mix.megawatts IS DISTINCT FROM EXCLUDED.megawatts
        RETURNING (xmax = 0) AS was_inserted
        """;

    private readonly string _connectionString;

    public FuelMixUpserter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<UpsertResult> UpsertAsync(
        IReadOnlyList<FuelMixEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return new UpsertResult(0, 0);
        }

        int inserted = 0;
        int updated = 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(UpsertSql, connection, transaction))
        {
            var pInterval = command.Parameters.Add("interval", NpgsqlDbType.TimestampTz);
            var pCategory = command.Parameters.Add("category", NpgsqlDbType.Text);
            var pMegawatts = command.Parameters.Add("megawatts", NpgsqlDbType.Numeric);

            foreach (var entry in entries)
            {
                pInterval.Value = entry.IntervalUtc;
                pCategory.Value = entry.Category;
                pMegawatts.Value = entry.Megawatts;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.GetBoolean(0))
                    {
                        inserted++;
                    }
                    else
                    {
                        updated++;
                    }
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new UpsertResult(inserted, updated);
    }
}

public record UpsertResult(int Inserted, int Updated);
