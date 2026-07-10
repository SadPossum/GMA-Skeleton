namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Scoping;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Generate a sample scoped report through the task runtime.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[ScopeAware]
public sealed record GenerateReportTaskPayload(string ReportName, int ExpectedRows) : ITaskPayload
{
    public const string TaskName = "generate-report";
    public const int PayloadVersion = 1;
}
