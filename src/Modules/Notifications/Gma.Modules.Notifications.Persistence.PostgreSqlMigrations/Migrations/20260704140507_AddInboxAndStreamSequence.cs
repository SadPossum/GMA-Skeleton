using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gma.Modules.Notifications.Persistence.PostgreSqlMigrations.Migrations
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
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
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
