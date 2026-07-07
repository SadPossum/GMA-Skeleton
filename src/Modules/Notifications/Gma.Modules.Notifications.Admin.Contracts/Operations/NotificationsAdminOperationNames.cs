namespace Gma.Modules.Notifications.Admin.Contracts;

using Gma.Modules.Notifications.Contracts;

public static class NotificationsAdminOperationNames
{
    public const string HistoryList = NotificationsModuleMetadata.Name + ".history.list";
    public const string HistoryGet = NotificationsModuleMetadata.Name + ".history.get";
    public const string HistoryStream = NotificationsModuleMetadata.Name + ".history.stream";
    public const string BroadcastsList = NotificationsModuleMetadata.Name + ".broadcasts.list";
    public const string BroadcastsCreate = NotificationsModuleMetadata.Name + ".broadcasts.create";
    public const string BroadcastsInboxList = NotificationsModuleMetadata.Name + ".broadcasts.inbox.list";
    public const string BroadcastsInboxStream = NotificationsModuleMetadata.Name + ".broadcasts.inbox.stream";
    public const string BroadcastsInboxMarkRead = NotificationsModuleMetadata.Name + ".broadcasts.inbox.mark-read";
}
