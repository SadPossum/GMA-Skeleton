namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class MarkAllNotificationBroadcastsReadCommandHandler(
    INotificationBroadcastRepository repository,
    ISystemClock clock)
    : ICommandHandler<MarkAllNotificationBroadcastsReadCommand, MarkAllNotificationBroadcastsReadResponse>
{
    public async Task<Result<MarkAllNotificationBroadcastsReadResponse>> HandleAsync(
        MarkAllNotificationBroadcastsReadCommand command,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(command.TenantId, command.RecipientKind, command.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<MarkAllNotificationBroadcastsReadResponse>(recipient.Error);
        }

        int updatedCount = await repository
            .MarkAllVisibleReadAsync(
                recipient.Value,
                clock.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new MarkAllNotificationBroadcastsReadResponse(updatedCount));
    }
}
