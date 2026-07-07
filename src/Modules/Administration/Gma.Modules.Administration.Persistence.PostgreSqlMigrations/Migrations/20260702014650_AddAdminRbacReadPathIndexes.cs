using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Administration.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRbacReadPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_principal_roles_PrincipalId_TenantId",
                schema: "admin",
                table: "principal_roles",
                columns: new[] { "PrincipalId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_principal_roles_PrincipalId_TenantId",
                schema: "admin",
                table: "principal_roles");
        }
    }
}
