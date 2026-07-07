using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialTaskRuntimeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tasks");

            migrationBuilder.CreateTable(
                name: "task_control_messages",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    EnqueuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_control_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_runs",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkerGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: true),
                    ProgressMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CancellationRequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationRequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_control_messages_RunId_Status_EnqueuedAtUtc",
                schema: "tasks",
                table: "task_control_messages",
                columns: new[] { "RunId", "Status", "EnqueuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_task_runs_ModuleName_TaskName",
                schema: "tasks",
                table: "task_runs",
                columns: new[] { "ModuleName", "TaskName" });

            migrationBuilder.CreateIndex(
                name: "IX_task_runs_WorkerGroup_Status_ScheduledAtUtc_NextAttemptAtUtc_LockedUntilUtc",
                schema: "tasks",
                table: "task_runs",
                columns: new[] { "WorkerGroup", "Status", "ScheduledAtUtc", "NextAttemptAtUtc", "LockedUntilUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_control_messages",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "task_runs",
                schema: "tasks");
        }
    }
}
