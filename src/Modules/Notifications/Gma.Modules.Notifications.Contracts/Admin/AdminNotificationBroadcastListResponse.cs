namespace Gma.Modules.Notifications.Contracts;

public sealed record AdminNotificationBroadcastListResponse(
    IReadOnlyList<AdminNotificationBroadcastItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
