namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class CreateNotificationBroadcastCommandHandler(
    INotificationBroadcastRepository repository,
    IIdGenerator idGenerator,
    ISystemClock clock)
    : ICommandHandler<CreateNotificationBroadcastCommand, AdminCreateNotificationBroadcastResponse>
{
    public async Task<Result<AdminCreateNotificationBroadcastResponse>> HandleAsync(
        CreateNotificationBroadcastCommand command,
        CancellationToken cancellationToken)
    {
        DateTimeOffset createdAtUtc = clock.UtcNow;
        Guid broadcastId = idGenerator.NewId();

        Result<NotificationBroadcast> broadcast = NotificationBroadcast.Create(
            broadcastId,
            command.TenantId,
            NotificationBroadcastAudienceMapper.ToDomainValue(command.Audience),
            command.Module,
            command.Name,
            command.Version,
            command.Title,
            command.Body,
            NotificationSeverityMapper.ToDomainValue(command.Severity),
            command.OccurredAtUtc ?? createdAtUtc,
            createdAtUtc,
            command.PayloadJson);

        if (broadcast.IsFailure)
        {
            return Result.Failure<AdminCreateNotificationBroadcastResponse>(broadcast.Error);
        }

        await repository.AddAsync(broadcast.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new AdminCreateNotificationBroadcastResponse(broadcastId));
    }
}
