using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Auth.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Handler = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Id, x.Handler });
                });

            migrationBuilder.CreateTable(
                name: "members",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "member_sessions",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RefreshTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LoginDateTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SignOutDateTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_sessions_members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "auth",
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_usernames",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UsernameType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_usernames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_usernames_members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "auth",
                        principalTable: "members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_sessions_MemberId",
                schema: "auth",
                table: "member_sessions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_member_sessions_TenantId_RefreshTokenHash",
                schema: "auth",
                table: "member_sessions",
                columns: new[] { "TenantId", "RefreshTokenHash" });

            migrationBuilder.CreateIndex(
                name: "IX_member_usernames_MemberId",
                schema: "auth",
                table: "member_usernames",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_member_usernames_TenantId_NormalizedValue",
                schema: "auth",
                table: "member_usernames",
                columns: new[] { "TenantId", "NormalizedValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilUtc_CreatedAtUtc",
                schema: "auth",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "member_sessions",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "member_usernames",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "members",
                schema: "auth");
        }
    }
}
