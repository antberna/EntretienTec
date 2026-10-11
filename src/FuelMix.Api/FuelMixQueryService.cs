using FuelMix.Api.Contracts;
using FuelMix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FuelMix.Api;

/// <summary>
/// Requêtes en lecture seule. Le DbContext est connecté avec le rôle
/// `fuelmix_report` (SELECT uniquement) : l'API ne peut pas écrire par construction.
/// </summary>
public class FuelMixQueryService
{
    private const int MaxPageSize = 1000;
    private const int DefaultPageSize = 100;

    private readonly FuelMixDbContext _db;

    public FuelMixQueryService(FuelMixDbContext db)
    {
        _db = db;
    }

    public async Task<LatestSnapshotDto?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var latest = await _db.FuelMix.AsNoTracking()
            .OrderByDescending(x => x.IntervalUtc)
            .Select(x => (DateTimeOffset?)x.IntervalUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return null; // table vide
        }

        var fuels = await _db.FuelMix.AsNoTracking()
            .Where(x => x.IntervalUtc == latest.Value)
            .OrderBy(x => x.Category)
            .Select(x => new FuelMixEntryDto(x.IntervalUtc, x.Category, x.Megawatts))
            .ToListAsync(cancellationToken);

        return new LatestSnapshotDto(latest.Value, fuels);
    }

    public async Task<PagedResultDto> QueryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.FuelMix.AsNoTracking();

        if (from is not null)
        {
            query = query.Where(x => x.IntervalUtc >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.IntervalUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.IntervalUtc)
            .ThenBy(x => x.Category)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FuelMixEntryDto(x.IntervalUtc, x.Category, x.Megawatts))
            .ToListAsync(cancellationToken);

        return new PagedResultDto(items, page, pageSize, totalCount);
    }
}
