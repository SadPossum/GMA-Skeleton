using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Notifications.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxAndStreamSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StreamSequence",
                schema: "notifications",
                table: "user_notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Handler = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Id, x.Handler });
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_TenantId_StreamSequence",
                schema: "notifications",
                table: "user_notifications",
                columns: new[] { "TenantId", "StreamSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_TenantId_UserId_StreamSequence",
                schema: "notifications",
                table: "user_notifications",
                columns: new[] { "TenantId", "UserId", "StreamSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "notifications",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_TenantId_StreamSequence",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_TenantId_UserId_StreamSequence",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropColumn(
                name: "StreamSequence",
                schema: "notifications",
                table: "user_notifications");
        }
    }
}
