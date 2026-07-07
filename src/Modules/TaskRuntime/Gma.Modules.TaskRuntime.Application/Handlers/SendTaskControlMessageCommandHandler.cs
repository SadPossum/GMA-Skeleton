namespace Gma.Modules.TaskRuntime.Application.Handlers;

using System.Text.Json;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;
using Gma.Modules.TaskRuntime.Application.Commands;

internal sealed class SendTaskControlMessageCommandHandler(
    ITaskRunStore store,
    IIdGenerator idGenerator,
    ISystemClock clock)
    : ICommandHandler<SendTaskControlMessageCommand, TaskControlMessage>
{
    public async Task<Result<TaskControlMessage>> HandleAsync(
        SendTaskControlMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RunId == Guid.Empty)
        {
            return Result.Failure<TaskControlMessage>(TaskRuntimeApplicationErrors.InvalidRunId);
        }

        if (!IsValidJson(command.PayloadJson))
        {
            return Result.Failure<TaskControlMessage>(TaskRuntimeApplicationErrors.InvalidPayloadJson);
        }

        TaskRunDetails? run = await store.GetAsync(command.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<TaskControlMessage>(TaskRuntimeApplicationErrors.RunNotFound);
        }

        if (TaskRunStatusTransitions.IsTerminal(run.Summary.Status))
        {
            return Result.Failure<TaskControlMessage>(TaskRuntimeApplicationErrors.RunCannotBeControlled);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        TaskControlMessage message;
        try
        {
            message = new TaskControlMessage(
                idGenerator.NewId(),
                command.RunId,
                command.CommandName,
                command.PayloadJson,
                nowUtc,
                command.RequestedBy,
                command.ExpiresAtUtc);
        }
        catch (ArgumentException)
        {
            return Result.Failure<TaskControlMessage>(TaskRuntimeApplicationErrors.InvalidControlMessage);
        }

        await store.EnqueueControlMessageAsync(message, cancellationToken).ConfigureAwait(false);

        return Result.Success(message);
    }

    private static bool IsValidJson(string payloadJson)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(payloadJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
