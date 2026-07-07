namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Demonstrate retry behavior by failing until a configured attempt.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[TenantScoped]
public sealed record FlakyReportTaskPayload(
    string ReportName,
    int ExpectedRows,
    int FailUntilAttempt) : ITaskPayload
{
    public const string TaskName = "flaky-report";
    public const int PayloadVersion = 1;
}
