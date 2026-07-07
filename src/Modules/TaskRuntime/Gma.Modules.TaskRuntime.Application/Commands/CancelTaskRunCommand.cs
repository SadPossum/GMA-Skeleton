namespace Gma.Modules.TaskRuntime.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record CancelTaskRunCommand(
    Guid RunId,
    string? RequestedBy) : ITransactionalCommand<Unit>;
