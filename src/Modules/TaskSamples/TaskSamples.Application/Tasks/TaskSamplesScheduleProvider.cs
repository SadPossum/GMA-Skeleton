namespace TaskSamples.Application.Tasks;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Gma.Framework.Tasks;
using TaskSamples.Contracts;

internal sealed class TaskSamplesScheduleProvider : ITaskScheduleProvider
{
    private static readonly string PayloadJson = JsonSerializer.Serialize(new GenerateReportTaskPayload("scheduled-daily", 10));

    public async IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ScheduledTaskDefinition(
            "scheduled-report",
            TaskSamplesModuleMetadata.Name,
            GenerateReportTaskPayload.TaskName,
            PayloadJson,
            TimeSpan.FromMinutes(5),
            TaskSamplesModuleMetadata.WorkerGroup,
            scopeId: "default",
            maxAttempts: 3,
            payloadVersion: GenerateReportTaskPayload.PayloadVersion);
    }
}
