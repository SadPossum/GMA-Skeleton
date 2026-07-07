namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Visibility;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StreamNotificationHistoryQueryHandler(
    INotificationHistoryRepository repository)
    : IQueryHandler<StreamNotificationHistoryQuery, IReadOnlyList<NotificationHistoryItem>>
{
    public async Task<Result<IReadOnlyList<NotificationHistoryItem>>> HandleAsync(
        StreamNotificationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!NotificationHistoryAccess.CanAccessUserHistory(query.Subject, query.Subject.TenantId))
        {
            return Result.Failure<IReadOnlyList<NotificationHistoryItem>>(NotificationsApplicationErrors.AccessDenied);
        }

        IReadOnlyList<NotificationHistoryItem> items = await repository
            .ListNewForUserAsync(
                query.Subject,
                query.AfterStreamSequence,
                query.BatchSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(items);
    }
}
