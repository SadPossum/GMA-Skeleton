namespace TaskSamples.Application.Tasks;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RecordTaskSampleReportCommandHandler(ITaskSampleReportSink sink)
    : ICommandHandler<RecordTaskSampleReportCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RecordTaskSampleReportCommand command,
        CancellationToken cancellationToken)
    {
        await sink.RecordAsync(
                new TaskSampleReport(
                    command.ReportName,
                    command.ExpectedRows,
                    command.RunId,
                    command.TenantId,
                    command.Attempt),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
