namespace TaskSamples.Application.Tasks;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using TaskSamples.Contracts;

internal sealed class FlakyReportTaskHandler(ITaskCommandDispatcher dispatcher)
    : ITaskHandler<FlakyReportTaskPayload>
{
    public async Task HandleAsync(
        FlakyReportTaskPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Attempt <= payload.FailUntilAttempt)
        {
            throw new InvalidOperationException($"Intentional sample failure on attempt {context.Attempt}.");
        }

        Result<Unit> result = await dispatcher.DispatchAsync<RecordTaskSampleReportCommand, Unit>(
                context,
                new RecordTaskSampleReportCommand(
                    payload.ReportName,
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
