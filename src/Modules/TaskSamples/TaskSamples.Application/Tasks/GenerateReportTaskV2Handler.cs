namespace TaskSamples.Application.Tasks;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using TaskSamples.Contracts;

internal sealed class GenerateReportTaskV2Handler(ITaskCommandDispatcher dispatcher)
    : ITaskHandler<GenerateReportTaskPayloadV2>
{
    public async Task HandleAsync(
        GenerateReportTaskPayloadV2 payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        Result<Unit> result = await dispatcher.DispatchAsync<RecordTaskSampleReportCommand, Unit>(
                context,
                new RecordTaskSampleReportCommand(
                    $"{payload.ReportName}.{payload.Format}",
                    payload.ExpectedRows,
                    context.RunId,
                    context.ScopeId ?? string.Empty,
                    context.Attempt),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Task sample report command failed with {result.Error.Code}: {result.Error.Message}");
        }
    }
}
