namespace Gma.Modules.Notifications.Contracts;

public sealed record AdminNotificationHistoryListResponse(
    IReadOnlyList<AdminNotificationHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount);
