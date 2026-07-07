namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListTenantNotificationBroadcastsQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<ListTenantNotificationBroadcastsQuery, AdminNotificationBroadcastListResponse>
{
    public async Task<Result<AdminNotificationBroadcastListResponse>> HandleAsync(
        ListTenantNotificationBroadcastsQuery query,
        CancellationToken cancellationToken)
    {
        AdminNotificationBroadcastListResponse response = await repository
            .ListTenantBroadcastsAsync(
                query.TenantId,
                PageRequest.Normalize(query.Page, query.PageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
