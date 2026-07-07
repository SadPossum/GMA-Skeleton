namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class MarkNotificationBroadcastReadCommandHandler(
    INotificationBroadcastRepository repository,
    ISystemClock clock)
    : ICommandHandler<MarkNotificationBroadcastReadCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        MarkNotificationBroadcastReadCommand command,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(command.TenantId, command.RecipientKind, command.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<Unit>(recipient.Error);
        }

        bool updated = await repository
            .MarkReadAsync(
                command.BroadcastId,
                recipient.Value,
                clock.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        return updated
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(NotificationsApplicationErrors.BroadcastNotFound);
    }
}
