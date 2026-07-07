namespace Gma.Modules.TaskRuntime.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Framework.Tasks;

public sealed record SendTaskControlMessageCommand(
    Guid RunId,
    string CommandName,
    string PayloadJson,
    DateTimeOffset? ExpiresAtUtc,
    string? RequestedBy) : ITransactionalCommand<TaskControlMessage>;
