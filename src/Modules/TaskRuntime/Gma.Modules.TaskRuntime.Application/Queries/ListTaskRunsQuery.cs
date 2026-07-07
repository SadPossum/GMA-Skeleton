namespace Gma.Modules.TaskRuntime.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;

public sealed record ListTaskRunsQuery(
    string? ModuleName,
    string? TaskName,
    string? WorkerGroup,
    TaskRunStatus? Status,
    string? TenantId,
    int Page,
    int PageSize) : IQuery<IReadOnlyList<TaskRunSummary>>;
