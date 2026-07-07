namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListNotificationBroadcastsQuery(
    string? TenantId,
    NotificationBroadcastRecipientKind RecipientKind,
    string RecipientId,
    bool UnreadOnly = false,
    int Page = Gma.Framework.Pagination.PageRequest.DefaultPage,
    int PageSize = Gma.Framework.Pagination.PageRequest.DefaultPageSize) : IQuery<NotificationBroadcastListResponse>;
