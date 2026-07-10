namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Scoping;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate a sample scoped report through the task runtime using the v2 payload contract.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[ScopeAware]
public sealed record GenerateReportTaskPayloadV2(
    string ReportName,
    int ExpectedRows,
    string Format) : ITaskPayload
{
    public const string TaskName = GenerateReportTaskPayload.TaskName;
    public const int PayloadVersion = 2;
}
