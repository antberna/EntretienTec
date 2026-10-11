namespace FuelMix.Infrastructure.Entities;

/// <summary>
/// Trace d'une exécution d'ingestion. Permet de distinguer :
///  - le « dernier import réussi » (max(finished_at) où status = 'success')
///  - la « fraîcheur réelle des données » (max(interval_utc) dans fuel_mix).
/// </summary>
public class ImportRun
{
    public long Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>running | success | failed</summary>
    public string Status { get; set; } = "running";

    /// <summary>live | today | yesterday | manual</summary>
    public string Source { get; set; } = default!;

    public int RowsReceived { get; set; }

    public int RowsInserted { get; set; }

    public int RowsUpdated { get; set; }

    public int RowsUnchanged { get; set; }

    public int RowsSkipped { get; set; }

    public string? Error { get; set; }
}
