using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.Auth.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberAdministrationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAtUtc",
                schema: "auth",
                table: "members",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                schema: "auth",
                table: "members",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAtUtc",
                schema: "auth",
                table: "members",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "auth",
                table: "members",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledAtUtc",
                schema: "auth",
                table: "members");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                schema: "auth",
                table: "members");

            migrationBuilder.DropColumn(
                name: "RegisteredAtUtc",
                schema: "auth",
                table: "members");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "auth",
                table: "members");
        }
    }
}
