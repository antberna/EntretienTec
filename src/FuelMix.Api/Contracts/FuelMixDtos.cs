namespace FuelMix.Api.Contracts;

public record FuelMixEntryDto(
    DateTimeOffset IntervalUtc,
    string Category,
    decimal Megawatts);

public record LatestSnapshotDto(
    DateTimeOffset IntervalUtc,
    IReadOnlyList<FuelMixEntryDto> Fuels);

public record PagedResultDto(
    IReadOnlyList<FuelMixEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
