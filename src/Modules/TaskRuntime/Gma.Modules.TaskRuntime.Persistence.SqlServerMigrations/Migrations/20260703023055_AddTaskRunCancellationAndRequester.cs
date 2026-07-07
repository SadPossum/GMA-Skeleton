using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRunCancellationAndRequester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                schema: "tasks",
                table: "task_runs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedBy",
                schema: "tasks",
                table: "task_runs");
        }
    }
}
