using FuelMix.Infrastructure.Data;
using FuelMix.Infrastructure.Entities;
using FuelMix.Infrastructure.Miso;
using Microsoft.Extensions.Logging;

namespace FuelMix.Infrastructure.Ingestion;

/// <summary>
/// Orchestration d'un import : fetch MISO -> mapping -> upsert transactionnel -> trace import_run.
/// Logique réutilisable par le Worker (périodique) et la commande manuelle (--once).
/// En cas d'échec, la trace import_run (status=failed) est enregistrée puis l'exception
/// est propagée à l'orchestrateur. Une annulation n'est pas tracée comme un échec.
/// </summary>
public class FuelMixIngestor
{
    private readonly MisoClient _client;
    private readonly FuelMixUpserter _upserter;
    private readonly FuelMixDbContext _db;
    private readonly ILogger<FuelMixIngestor> _logger;

    public FuelMixIngestor(
        MisoClient client,
        FuelMixUpserter upserter,
        FuelMixDbContext db,
        ILogger<FuelMixIngestor> logger)
    {
        _client = client;
        _upserter = upserter;
        _db = db;
        _logger = logger;
    }

    public async Task<IngestResult> IngestAsync(
        FuelMixSource source,
        CancellationToken cancellationToken = default)
    {
        var run = new ImportRun
        {
            StartedAt = DateTimeOffset.UtcNow,
            Status = "running",
            Source = source.ToString().ToLowerInvariant()
        };

        try
        {
            var rows = await _client.GetFuelMixAsync(source, cancellationToken);
            run.RowsReceived = rows.Count;

            var entries = new List<FuelMixEntry>(rows.Count);
            int skipped = 0;

            foreach (var row in rows)
            {
                if (MisoMapper.TryMap(row, out var entry, out var reason))
                {
                    entries.Add(entry!);
                }
                else
                {
                    skipped++;
                    _logger.LogWarning("Skipped row (source {Source}): {Reason}", run.Source, reason);
                }
            }

            run.RowsSkipped = skipped;

            var upsert = await _upserter.UpsertAsync(entries, cancellationToken);
            run.RowsInserted = upsert.Inserted;
            run.RowsUpdated = upsert.Updated;
            run.RowsUnchanged = entries.Count - upsert.Inserted - upsert.Updated;
            run.Status = "success";
        }
        catch (OperationCanceledException)
        {
            // Annulation (ex. arrêt du service) : pas un échec métier, on ne trace rien.
            throw;
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.Error = ex.Message;
            _logger.LogError(ex, "Ingestion failed (source {Source})", run.Source);
            await RecordRunAsync(run);
            throw; // propagée à l'orchestrateur
        }

        await RecordRunAsync(run);

        _logger.LogInformation(
            "Ingestion {Status} (source {Source}): received={Received} inserted={Inserted} updated={Updated} unchanged={Unchanged} skipped={Skipped}",
            run.Status, run.Source, run.RowsReceived, run.RowsInserted, run.RowsUpdated,
            run.RowsUnchanged, run.RowsSkipped);

        return new IngestResult(
            run.Status, run.Source, run.RowsReceived, run.RowsInserted,
            run.RowsUpdated, run.RowsUnchanged, run.RowsSkipped, run.Error);
    }

    /// <summary>Trace l'exécution (best-effort) sans masquer une exception en cours.</summary>
    private async Task RecordRunAsync(ImportRun run)
    {
        try
        {
            run.FinishedAt = DateTimeOffset.UtcNow;
            _db.ImportRuns.Add(run);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record import_run");
        }
    }
}
