using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Administration.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdministrationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admin");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "principals",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_principals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "principal_roles",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_principal_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_principal_roles_principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalSchema: "admin",
                        principalTable: "principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_principal_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "admin",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "admin",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_ActorId_CreatedAtUtc",
                schema: "admin",
                table: "audit_entries",
                columns: new[] { "ActorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_TenantId_CreatedAtUtc",
                schema: "admin",
                table: "audit_entries",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_roles_PrincipalId_RoleId_TenantId",
                schema: "admin",
                table: "principal_roles",
                columns: new[] { "PrincipalId", "RoleId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_roles_RoleId",
                schema: "admin",
                table: "principal_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_RoleId_PermissionCode",
                schema: "admin",
                table: "role_permissions",
                columns: new[] { "RoleId", "PermissionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                schema: "admin",
                table: "roles",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "principal_roles",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "principals",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "admin");
        }
    }
}
