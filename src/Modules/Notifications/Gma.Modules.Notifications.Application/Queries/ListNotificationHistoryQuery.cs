namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;

public sealed record ListNotificationHistoryQuery(
    AccessSubject Subject,
    bool UnreadOnly = false,
    int Page = PageRequest.DefaultPage,
    int PageSize = PageRequest.DefaultPageSize)
    : IQuery<NotificationHistoryListResponse>;
