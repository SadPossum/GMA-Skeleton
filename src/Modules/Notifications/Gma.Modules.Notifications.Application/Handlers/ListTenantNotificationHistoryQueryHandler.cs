namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListTenantNotificationHistoryQueryHandler(INotificationHistoryRepository repository)
    : IQueryHandler<ListTenantNotificationHistoryQuery, AdminNotificationHistoryListResponse>
{
    public async Task<Result<AdminNotificationHistoryListResponse>> HandleAsync(
        ListTenantNotificationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        AdminNotificationHistoryListResponse response = await repository
            .ListTenantAsync(query.UserId, query.UnreadOnly, pageRequest, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
