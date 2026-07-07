using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRunPayloadVersionAndDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                schema: "tasks",
                table: "task_runs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayloadVersion",
                schema: "tasks",
                table: "task_runs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_task_runs_ModuleName_TaskName_TenantId_DeduplicationKey_Status",
                schema: "tasks",
                table: "task_runs",
                columns: new[] { "ModuleName", "TaskName", "TenantId", "DeduplicationKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_runs_ModuleName_TaskName_TenantId_DeduplicationKey_Status",
                schema: "tasks",
                table: "task_runs");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                schema: "tasks",
                table: "task_runs");

            migrationBuilder.DropColumn(
                name: "PayloadVersion",
                schema: "tasks",
                table: "task_runs");
        }
    }
}
