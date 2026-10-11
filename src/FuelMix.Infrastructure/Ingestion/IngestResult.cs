namespace FuelMix.Infrastructure.Ingestion;

public record IngestResult(
    string Status,
    string Source,
    int RowsReceived,
    int RowsInserted,
    int RowsUpdated,
    int RowsUnchanged,
    int RowsSkipped,
    string? Error);
