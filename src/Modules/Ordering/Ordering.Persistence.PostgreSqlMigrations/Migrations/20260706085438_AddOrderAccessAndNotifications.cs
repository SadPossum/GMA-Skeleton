using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAccessAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegionCode",
                schema: "ordering",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "GLOBAL");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "ordering",
                table: "orders",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "legacy-unassigned");

            migrationBuilder.AddColumn<string>(
                name: "AvailableRegionCodes",
                schema: "ordering",
                table: "catalog_item_projections",
                type: "character varying(1055)",
                maxLength: 1055,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ScopeId_UserId_CreatedAtUtc",
                schema: "ordering",
                table: "orders",
                columns: new[] { "ScopeId", "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "ordering",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "ordering");

            migrationBuilder.DropIndex(
                name: "IX_orders_ScopeId_UserId_CreatedAtUtc",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "RegionCode",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "AvailableRegionCodes",
                schema: "ordering",
                table: "catalog_item_projections");
        }
    }
}
