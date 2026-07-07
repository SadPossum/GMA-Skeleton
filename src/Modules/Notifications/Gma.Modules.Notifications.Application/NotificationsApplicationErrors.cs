namespace Gma.Modules.Notifications.Application;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public static class NotificationsApplicationErrors
{
    public static readonly Error NotificationNotFound = NotificationsDomainErrors.NotificationNotFound;
    public static readonly Error BroadcastNotFound = NotificationsDomainErrors.BroadcastNotFound;
    public static readonly Error AccessDenied = new("Notifications.AccessDenied", "Notification access is denied.");
    public static readonly Error StreamCursorInvalid = new("Notifications.StreamCursorInvalid", "Notification stream cursor is invalid.");
}
