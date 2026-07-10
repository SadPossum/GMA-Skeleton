namespace TaskSamples.Contracts;

using Gma.Framework.Tasks;
using Gma.Framework.Scoping;

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Demonstrate long-running task progress, heartbeat reporting, and cooperative control.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(TaskSamplesModuleMetadata.WorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
public sealed record SlowReportTaskPayload(
    string ReportName,
    int ExpectedRows,
    int Steps,
    int DelayMilliseconds) : ITaskPayload
{
    public const string TaskName = "slow-report";
    public const int PayloadVersion = 1;
}
