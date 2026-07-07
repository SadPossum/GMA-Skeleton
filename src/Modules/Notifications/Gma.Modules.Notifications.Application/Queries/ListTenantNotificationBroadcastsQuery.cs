namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListTenantNotificationBroadcastsQuery(
    string TenantId,
    int Page = Gma.Framework.Pagination.PageRequest.DefaultPage,
    int PageSize = Gma.Framework.Pagination.PageRequest.DefaultPageSize) : IQuery<AdminNotificationBroadcastListResponse>;
