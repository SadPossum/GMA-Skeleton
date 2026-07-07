namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListNotificationBroadcastsQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<ListNotificationBroadcastsQuery, NotificationBroadcastListResponse>
{
    public async Task<Result<NotificationBroadcastListResponse>> HandleAsync(
        ListNotificationBroadcastsQuery query,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(query.TenantId, query.RecipientKind, query.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<NotificationBroadcastListResponse>(recipient.Error);
        }

        NotificationBroadcastListResponse response = await repository
            .ListVisibleAsync(
                recipient.Value,
                query.UnreadOnly,
                PageRequest.Normalize(query.Page, query.PageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
