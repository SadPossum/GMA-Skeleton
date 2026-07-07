namespace Gma.Modules.TaskRuntime.Application.Queries;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;

public sealed record GetTaskRunQuery(Guid RunId) : IQuery<TaskRunDetails>;
