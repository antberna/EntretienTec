using FuelMix.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace FuelMix.Infrastructure.Data;

public class FuelMixDbContext : DbContext
{
    public FuelMixDbContext(DbContextOptions<FuelMixDbContext> options) : base(options)
    {
    }

    public DbSet<FuelMixEntry> FuelMix => Set<FuelMixEntry>();

    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fuelmix");

        modelBuilder.Entity<FuelMixEntry>(entity =>
        {
            entity.ToTable("fuel_mix");

            entity.HasKey(e => new { e.IntervalUtc, e.Category });

            entity.Property(e => e.IntervalUtc)
                .HasColumnName("interval_utc")
                .HasColumnType("timestamptz");

            entity.Property(e => e.Category)
                .HasColumnName("category")
                .HasMaxLength(64);

            entity.Property(e => e.Megawatts)
                .HasColumnName("megawatts")
                .HasPrecision(10, 3);

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz");

            // Séries temporelles par catégorie
            entity.HasIndex(e => new { e.Category, e.IntervalUtc })
                .HasDatabaseName("ix_fuel_mix_category_interval");
        });

        modelBuilder.Entity<ImportRun>(entity =>
        {
            entity.ToTable("import_run");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseIdentityByDefaultColumn();

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at")
                .HasColumnType("timestamptz");

            entity.Property(e => e.FinishedAt)
                .HasColumnName("finished_at")
                .HasColumnType("timestamptz");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(16);

            entity.Property(e => e.Source)
                .HasColumnName("source")
                .HasMaxLength(16);

            entity.Property(e => e.RowsReceived).HasColumnName("rows_received");
            entity.Property(e => e.RowsInserted).HasColumnName("rows_inserted");
            entity.Property(e => e.RowsUpdated).HasColumnName("rows_updated");
            entity.Property(e => e.RowsUnchanged).HasColumnName("rows_unchanged");
            entity.Property(e => e.RowsSkipped).HasColumnName("rows_skipped");

            entity.Property(e => e.Error).HasColumnName("error");
        });
    }
}
