using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Auth.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInboxMessageShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ProcessedAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                schema: "auth",
                table: "inbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                schema: "auth",
                table: "inbox_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                schema: "auth",
                table: "inbox_messages",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                schema: "auth",
                table: "inbox_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingStartedAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "auth",
                table: "inbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                schema: "auth",
                table: "inbox_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "auth",
                table: "inbox_messages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "auth",
                table: "inbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "auth",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "Attempts",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "EventType",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAtUtc",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "Subject",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "auth",
                table: "inbox_messages");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ProcessedAtUtc",
                schema: "auth",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
