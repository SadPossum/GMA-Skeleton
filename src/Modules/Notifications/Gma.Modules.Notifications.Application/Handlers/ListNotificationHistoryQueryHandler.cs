namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Visibility;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListNotificationHistoryQueryHandler(
    INotificationHistoryRepository repository)
    : IQueryHandler<ListNotificationHistoryQuery, NotificationHistoryListResponse>
{
    public async Task<Result<NotificationHistoryListResponse>> HandleAsync(
        ListNotificationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!NotificationHistoryAccess.CanAccessUserHistory(query.Subject, query.Subject.TenantId))
        {
            return Result.Failure<NotificationHistoryListResponse>(NotificationsApplicationErrors.AccessDenied);
        }

        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        NotificationHistoryListResponse response = await repository
            .ListAsync(query.Subject, query.UnreadOnly, pageRequest, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
