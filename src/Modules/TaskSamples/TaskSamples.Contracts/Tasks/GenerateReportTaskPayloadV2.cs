namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate a sample tenant report through the task runtime using the v2 payload contract.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[TenantScoped]
public sealed record GenerateReportTaskPayloadV2(
    string ReportName,
    int ExpectedRows,
    string Format) : ITaskPayload
{
    public const string TaskName = GenerateReportTaskPayload.TaskName;
    public const int PayloadVersion = 2;
}
