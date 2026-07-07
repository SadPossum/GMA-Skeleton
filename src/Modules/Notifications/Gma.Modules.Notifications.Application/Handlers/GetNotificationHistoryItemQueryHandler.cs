namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetNotificationHistoryItemQueryHandler(
    INotificationHistoryRepository repository)
    : IQueryHandler<GetNotificationHistoryItemQuery, NotificationHistoryItem>
{
    public async Task<Result<NotificationHistoryItem>> HandleAsync(
        GetNotificationHistoryItemQuery query,
        CancellationToken cancellationToken)
    {
        NotificationHistoryItem? notification = await repository
            .GetAsync(query.NotificationId, query.Subject, cancellationToken)
            .ConfigureAwait(false);

        return notification is null
            ? Result.Failure<NotificationHistoryItem>(NotificationsDomainErrors.NotificationNotFound)
            : Result.Success(notification);
    }
}
