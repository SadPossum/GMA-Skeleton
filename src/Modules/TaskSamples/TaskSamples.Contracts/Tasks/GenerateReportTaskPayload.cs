namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate a sample tenant report through the task runtime.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[TenantScoped]
public sealed record GenerateReportTaskPayload(string ReportName, int ExpectedRows) : ITaskPayload
{
    public const string TaskName = "generate-report";
    public const int PayloadVersion = 1;
}
