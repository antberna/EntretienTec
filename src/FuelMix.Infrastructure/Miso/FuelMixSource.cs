namespace FuelMix.Infrastructure.Miso;

/// <summary>
/// Endpoints MISO supportés par l'ingestion :
///  - Live      : /api/FuelMix (ingestion périodique)
///  - Today     : /api/FuelMix/Today (backfill manuel)
///  - Yesterday : /api/FuelMix/Yesterday (backfill manuel)
/// </summary>
public enum FuelMixSource
{
    Live,
    Today,
    Yesterday
}
