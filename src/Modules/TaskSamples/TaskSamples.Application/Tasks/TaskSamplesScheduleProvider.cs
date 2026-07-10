namespace TaskSamples.Application.Tasks;

using System.Text.Json;
using Gma.Framework.Tasks;
using TaskSamples.Contracts;

internal sealed class TaskSamplesScheduleProvider : ITaskScheduleProvider
{
    private static readonly string PayloadJson = JsonSerializer.Serialize(new GenerateReportTaskPayload("scheduled-daily", 10));

    public Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScheduledTaskDefinition>>(
        [
            new ScheduledTaskDefinition(
                "scheduled-report",
                TaskSamplesModuleMetadata.Name,
                GenerateReportTaskPayload.TaskName,
                PayloadJson,
                TimeSpan.FromMinutes(5),
                TaskSamplesModuleMetadata.WorkerGroup,
                scopeId: "default",
                maxAttempts: 3,
                payloadVersion: GenerateReportTaskPayload.PayloadVersion)
        ]);
}
