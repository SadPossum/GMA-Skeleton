using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Gma.Modules.Notifications.Persistence;

#nullable disable

namespace Gma.Modules.Notifications.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NotificationsDbContext))]
    [Migration("20260705121200_AddInboxStatusUnknown")]
    public partial class AddInboxStatusUnknown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [notifications].[inbox_messages]
                SET [Status] = [Status] + 1
                WHERE [Status] IN (0, 1, 2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [notifications].[inbox_messages]
                SET [Status] = [Status] - 1
                WHERE [Status] IN (1, 2, 3, 4);
                """);
        }
    }
}
