using Gma.Modules.Auth.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Auth.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260705121030_AddInboxStatusUnknown")]
    public partial class AddInboxStatusUnknown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "auth"."inbox_messages"
                SET "Status" = "Status" + 1
                WHERE "Status" IN (0, 1, 2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "auth"."inbox_messages"
                SET "Status" = "Status" - 1
                WHERE "Status" IN (1, 2, 3, 4);
                """);
        }
    }
}
