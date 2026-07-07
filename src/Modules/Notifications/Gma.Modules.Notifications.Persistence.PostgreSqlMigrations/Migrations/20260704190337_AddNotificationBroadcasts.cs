using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gma.Modules.Notifications.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                schema: "notifications",
                table: "user_notifications",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "notification_broadcast_reads",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientScope = table.Column<string>(type: "character varying(135)", maxLength: 135, nullable: false),
                    RecipientKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_broadcast_reads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_broadcasts",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Audience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StreamSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_broadcasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_broadcast_reads_BroadcastId_RecipientScope_Rec~",
                schema: "notifications",
                table: "notification_broadcast_reads",
                columns: new[] { "BroadcastId", "RecipientScope", "RecipientKind", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_broadcast_reads_RecipientScope_RecipientKind_R~",
                schema: "notifications",
                table: "notification_broadcast_reads",
                columns: new[] { "RecipientScope", "RecipientKind", "RecipientId", "BroadcastId" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_broadcasts_Audience_TenantId_OccurredAtUtc",
                schema: "notifications",
                table: "notification_broadcasts",
                columns: new[] { "Audience", "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_broadcasts_Audience_TenantId_StreamSequence",
                schema: "notifications",
                table: "notification_broadcasts",
                columns: new[] { "Audience", "TenantId", "StreamSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_broadcasts_Module_Name_Version",
                schema: "notifications",
                table: "notification_broadcasts",
                columns: new[] { "Module", "Name", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_broadcast_reads",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_broadcasts",
                schema: "notifications");

            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                schema: "notifications",
                table: "user_notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32768)",
                oldMaxLength: 32768);
        }
    }
}
