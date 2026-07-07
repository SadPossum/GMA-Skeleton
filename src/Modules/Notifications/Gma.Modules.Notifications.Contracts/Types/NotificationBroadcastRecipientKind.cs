namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(NotificationBroadcastRecipientKindJsonConverter))]
public enum NotificationBroadcastRecipientKind
{
    Unknown = 0,
    User = 1,
    Admin = 2
}
