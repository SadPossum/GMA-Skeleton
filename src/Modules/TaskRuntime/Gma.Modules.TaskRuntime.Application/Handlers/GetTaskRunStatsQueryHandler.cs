namespace Gma.Modules.TaskRuntime.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;
using Gma.Framework.Results;
using Gma.Modules.TaskRuntime.Application.Queries;

internal sealed class GetTaskRunStatsQueryHandler(ITaskRunStore store)
    : IQueryHandler<GetTaskRunStatsQuery, TaskRunStats>
{
    public async Task<Result<TaskRunStats>> HandleAsync(
        GetTaskRunStatsQuery query,
        CancellationToken cancellationToken)
    {
        TaskRunStats stats = await store.GetStatsAsync(
                new TaskRunStatsFilter(
                    query.ModuleName,
                    query.TaskName,
                    query.WorkerGroup,
                    query.TenantId),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(stats);
    }
}
