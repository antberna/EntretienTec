using System.Globalization;
using FuelMix.Api;
using FuelMix.Api.Contracts;
using FuelMix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((_, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var reportConnection = builder.Configuration["ConnectionStrings:Report"]
    ?? throw new InvalidOperationException("ConnectionStrings:Report is required.");

// Contexte en lecture seule : rôle `fuelmix_report` (SELECT uniquement).
builder.Services.AddDbContext<FuelMixDbContext>(options => options.UseNpgsql(reportConnection));
builder.Services.AddScoped<FuelMixQueryService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter()));

var app = builder.Build();

// Journalise les requêtes sans corps (aucun secret dans les logs).
app.UseSerilogRequestLogging();

// Health check non authentifié : vérifie la connectivité à la base (rôle report).
app.MapGet("/health", async (FuelMixDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "Healthy" })
            : Results.Json(new { status = "Unhealthy" }, statusCode: 503);
    }
    catch
    {
        return Results.Json(new { status = "Unhealthy" }, statusCode: 503);
    }
});

var fuelMix = app.MapGroup("/api/fuelmix")
    .AddEndpointFilter<ApiKeyEndpointFilter>();

fuelMix.MapGet("/latest", async (FuelMixQueryService service, CancellationToken ct) =>
{
    var snapshot = await service.GetLatestAsync(ct);
    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
});

fuelMix.MapGet("", async (
    FuelMixQueryService service,
    string? from,
    string? to,
    string? category,
    int? page,
    int? pageSize,
    CancellationToken ct) =>
{
    if (!TryParseUtc(from, out var fromUtc) || !TryParseUtc(to, out var toUtc))
    {
        return Results.BadRequest("'from' and 'to' must be valid ISO 8601 dates.");
    }

    if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
    {
        return Results.BadRequest("'from' must be less than or equal to 'to'.");
    }

    var result = await service.QueryAsync(
        fromUtc, toUtc, category, page ?? 1, pageSize ?? 100, ct);

    return Results.Ok(result);
});

app.Run();

static bool TryParseUtc(string? value, out DateTimeOffset? result)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        result = null;
        return true;
    }

    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
    {
        result = parsed;
        return true;
    }

    result = null;
    return false;
}
