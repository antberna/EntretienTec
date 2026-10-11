using FuelMix.Infrastructure.Data;
using FuelMix.Infrastructure.Ingestion;
using FuelMix.Infrastructure.Miso;
using FuelMix.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

// --- Mode ponctuel (optionnel) : --once [live|today|yesterday] ---
var once = args.Contains("--once");
var sourceArg = args.FirstOrDefault(a => a is "live" or "today" or "yesterday") ?? "live";
var source = sourceArg switch
{
    "today" => FuelMixSource.Today,
    "yesterday" => FuelMixSource.Yesterday,
    _ => FuelMixSource.Live
};

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var ingestConnection = builder.Configuration["ConnectionStrings:Ingest"]
    ?? throw new InvalidOperationException("ConnectionStrings:Ingest is required.");
var migratorConnection = builder.Configuration["ConnectionStrings:Migrator"] ?? ingestConnection;

builder.Services.AddDbContext<FuelMixDbContext>(options =>
    options.UseNpgsql(ingestConnection, NpgsqlDefaults.Configure));

builder.Services.AddSingleton(new FuelMixUpserter(ingestConnection));

builder.Services.AddSingleton(_ => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["Miso:BaseUrl"] ?? "https://public-api.misoenergy.org/"),
    Timeout = TimeSpan.FromSeconds(30)
});
builder.Services.AddSingleton<MisoClient>();
builder.Services.AddScoped<FuelMixIngestor>();

builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();

// Applique les migrations avec le rôle dédié (DDL), distinct du rôle d'ingestion.
DatabaseMigrator.Migrate(migratorConnection);

if (once)
{
    using var scope = host.Services.CreateScope();
    var ingestor = scope.ServiceProvider.GetRequiredService<FuelMixIngestor>();
    var result = await ingestor.IngestAsync(source);
    return result.Status == "success" ? 0 : 1;
}

await host.RunAsync();
return 0;
