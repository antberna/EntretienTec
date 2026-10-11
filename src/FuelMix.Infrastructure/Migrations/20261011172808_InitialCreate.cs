using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FuelMix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fuelmix");

            migrationBuilder.CreateTable(
                name: "fuel_mix",
                schema: "fuelmix",
                columns: table => new
                {
                    interval_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    megawatts = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_mix", x => new { x.interval_utc, x.category });
                });

            migrationBuilder.CreateTable(
                name: "import_run",
                schema: "fuelmix",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rows_received = table.Column<int>(type: "integer", nullable: false),
                    rows_inserted = table.Column<int>(type: "integer", nullable: false),
                    rows_updated = table.Column<int>(type: "integer", nullable: false),
                    rows_unchanged = table.Column<int>(type: "integer", nullable: false),
                    rows_skipped = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_run", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fuel_mix_category_interval",
                schema: "fuelmix",
                table: "fuel_mix",
                columns: new[] { "category", "interval_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fuel_mix",
                schema: "fuelmix");

            migrationBuilder.DropTable(
                name: "import_run",
                schema: "fuelmix");
        }
    }
}
