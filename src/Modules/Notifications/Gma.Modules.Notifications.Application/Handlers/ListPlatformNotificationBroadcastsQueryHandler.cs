namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListPlatformNotificationBroadcastsQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<ListPlatformNotificationBroadcastsQuery, AdminNotificationBroadcastListResponse>
{
    public async Task<Result<AdminNotificationBroadcastListResponse>> HandleAsync(
        ListPlatformNotificationBroadcastsQuery query,
        CancellationToken cancellationToken)
    {
        AdminNotificationBroadcastListResponse response = await repository
            .ListPlatformBroadcastsAsync(
                PageRequest.Normalize(query.Page, query.PageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
