using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Auth.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthReadPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_members_TenantId_RegisteredAtUtc",
                schema: "auth",
                table: "members",
                columns: new[] { "TenantId", "RegisteredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_members_TenantId_RegisteredAtUtc",
                schema: "auth",
                table: "members");
        }
    }
}
