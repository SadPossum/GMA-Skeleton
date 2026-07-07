namespace Gma.Modules.TaskRuntime.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;
using Gma.Modules.TaskRuntime.Application.Commands;

internal sealed class CancelTaskRunCommandHandler(
    ITaskRunStore store,
    ISystemClock clock)
    : ICommandHandler<CancelTaskRunCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CancelTaskRunCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RunId == Guid.Empty)
        {
            return Result.Failure<Unit>(TaskRuntimeApplicationErrors.InvalidRunId);
        }

        TaskRunDetails? run = await store.GetAsync(command.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<Unit>(TaskRuntimeApplicationErrors.RunNotFound);
        }

        TaskRunStatus status = run.Summary.Status;
        if (status is TaskRunStatus.Canceled or TaskRunStatus.CancellationRequested)
        {
            return Result.Success(Unit.Value);
        }

        if (!TaskRunStatusTransitions.CanRequestCancellation(status))
        {
            return Result.Failure<Unit>(TaskRuntimeApplicationErrors.RunCannotBeCanceled);
        }

        await store.RequestCancellationAsync(
                command.RunId,
                command.RequestedBy,
                clock.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
