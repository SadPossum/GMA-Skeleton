namespace Gma.Modules.TaskRuntime.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;

public sealed record GetTaskRunStatsQuery(
    string? ModuleName,
    string? TaskName,
    string? WorkerGroup,
    string? TenantId) : IQuery<TaskRunStats>;
