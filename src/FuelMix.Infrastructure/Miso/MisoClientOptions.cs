namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Options de résilience du client MISO (bindées depuis la configuration "Miso:*").
/// </summary>
public sealed class MisoClientOptions
{
    /// <summary>Nombre total de tentatives HTTP (1 première + retries). Défaut : 3.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>Délai de base du backoff exponentiel (ms). Défaut : 1000.</summary>
    public int RetryBaseDelayMs { get; init; } = 1000;

    /// <summary>Budget total alloué à une ingestion (s) ; au-delà, on abandonne proprement.</summary>
    public int ExecutionBudgetSeconds { get; init; } = 30;

    public TimeSpan RetryBaseDelay => TimeSpan.FromMilliseconds(RetryBaseDelayMs);

    public TimeSpan ExecutionBudget => TimeSpan.FromSeconds(ExecutionBudgetSeconds);
}
