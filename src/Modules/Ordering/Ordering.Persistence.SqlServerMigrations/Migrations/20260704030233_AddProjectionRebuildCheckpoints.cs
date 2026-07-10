using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Persistence.SqlServerMigrations.Migrations
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
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProjectionName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Cursor = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    WrittenCount = table.Column<long>(type: "bigint", nullable: false),
                    SkippedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
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
