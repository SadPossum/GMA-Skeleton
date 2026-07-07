namespace Gma.Modules.Notifications.Contracts;

public sealed record NotificationBroadcastListResponse(
    IReadOnlyList<NotificationBroadcastItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount);
