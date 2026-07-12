using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxRetentionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Status_ProcessedAtUtc",
                schema: "catalog",
                table: "inbox_messages",
                columns: new[] { "Status", "ProcessedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_Status_ProcessedAtUtc",
                schema: "catalog",
                table: "inbox_messages");
        }
    }
}
