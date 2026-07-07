namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetTenantNotificationHistoryItemQueryHandler(INotificationHistoryRepository repository)
    : IQueryHandler<GetTenantNotificationHistoryItemQuery, AdminNotificationHistoryItem>
{
    public async Task<Result<AdminNotificationHistoryItem>> HandleAsync(
        GetTenantNotificationHistoryItemQuery query,
        CancellationToken cancellationToken)
    {
        AdminNotificationHistoryItem? notification = await repository
            .GetTenantAsync(query.NotificationId, cancellationToken)
            .ConfigureAwait(false);

        return notification is null
            ? Result.Failure<AdminNotificationHistoryItem>(NotificationsApplicationErrors.NotificationNotFound)
            : Result.Success(notification);
    }
}
