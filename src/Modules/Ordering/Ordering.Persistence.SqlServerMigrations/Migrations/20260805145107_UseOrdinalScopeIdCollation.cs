using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class UseOrdinalScopeIdCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropScopeIdDependencies(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "projection_rebuild_checkpoints",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "outbox_messages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "orders",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "inbox_messages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "catalog_item_projections",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            CreateScopeIdDependencies(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropScopeIdDependencies(migrationBuilder);

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "projection_rebuild_checkpoints",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "outbox_messages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "orders",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "inbox_messages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "ScopeId",
                schema: "ordering",
                table: "catalog_item_projections",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldCollation: "Latin1_General_100_BIN2");

            CreateScopeIdDependencies(migrationBuilder);
        }

        private static void DropScopeIdDependencies(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_projection_rebuild_checkpoints",
                schema: "ordering",
                table: "projection_rebuild_checkpoints");

            migrationBuilder.DropIndex(
                name: "IX_orders_ScopeId_CatalogItemId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_ScopeId_UserId_CreatedAtUtc",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_catalog_item_projections_ScopeId_CatalogItemId",
                schema: "ordering",
                table: "catalog_item_projections");
        }

        private static void CreateScopeIdDependencies(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey(
                name: "PK_projection_rebuild_checkpoints",
                schema: "ordering",
                table: "projection_rebuild_checkpoints",
                columns: new[] { "ScopeId", "ProjectionName", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ScopeId_CatalogItemId",
                schema: "ordering",
                table: "orders",
                columns: new[] { "ScopeId", "CatalogItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_ScopeId_UserId_CreatedAtUtc",
                schema: "ordering",
                table: "orders",
                columns: new[] { "ScopeId", "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_item_projections_ScopeId_CatalogItemId",
                schema: "ordering",
                table: "catalog_item_projections",
                columns: new[] { "ScopeId", "CatalogItemId" },
                unique: true);
        }
    }
}
