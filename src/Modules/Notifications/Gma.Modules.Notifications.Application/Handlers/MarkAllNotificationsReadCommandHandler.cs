namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Visibility;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class MarkAllNotificationsReadCommandHandler(
    INotificationHistoryRepository repository,
    ISystemClock clock)
    : ICommandHandler<MarkAllNotificationsReadCommand, MarkAllNotificationsReadResponse>
{
    public async Task<Result<MarkAllNotificationsReadResponse>> HandleAsync(
        MarkAllNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        if (!NotificationHistoryAccess.CanAccessUserHistory(command.Subject, command.Subject.TenantId))
        {
            return Result.Failure<MarkAllNotificationsReadResponse>(NotificationsApplicationErrors.AccessDenied);
        }

        int updatedCount = await repository
            .MarkAllReadAsync(command.Subject, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new MarkAllNotificationsReadResponse(updatedCount));
    }
}
