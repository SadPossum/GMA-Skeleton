namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetNotificationBroadcastQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<GetNotificationBroadcastQuery, NotificationBroadcastItem>
{
    public async Task<Result<NotificationBroadcastItem>> HandleAsync(
        GetNotificationBroadcastQuery query,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(query.TenantId, query.RecipientKind, query.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<NotificationBroadcastItem>(recipient.Error);
        }

        NotificationBroadcastItem? broadcast = await repository
            .GetVisibleAsync(query.BroadcastId, recipient.Value, cancellationToken)
            .ConfigureAwait(false);

        return broadcast is null
            ? Result.Failure<NotificationBroadcastItem>(NotificationsApplicationErrors.BroadcastNotFound)
            : Result.Success(broadcast);
    }
}
