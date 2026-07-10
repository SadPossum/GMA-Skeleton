using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionRebuildCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projection_rebuild_checkpoints",
                schema: "ordering",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProjectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    WrittenCount = table.Column<long>(type: "bigint", nullable: false),
                    SkippedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_rebuild_checkpoints", x => new { x.ScopeId, x.ProjectionName, x.RunId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "ordering");
        }
    }
}
