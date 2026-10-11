using FuelMix.Infrastructure.Ingestion;
using FuelMix.Infrastructure.Miso;

namespace FuelMix.Worker;

/// <summary>
/// Ingestion périodique : appelle l'import toutes les <c>Ingest:IntervalSeconds</c>
/// (défaut 300 s). Un scope est créé à chaque itération pour isoler le DbContext (scoped).
/// </summary>
public class IngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<IngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue("Ingest:IntervalSeconds", 300);
        if (intervalSeconds <= 0)
        {
            intervalSeconds = 300;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arrêt normal
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<FuelMixIngestor>();
            await ingestor.IngestAsync(FuelMixSource.Live, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during periodic ingestion");
        }
    }
}
