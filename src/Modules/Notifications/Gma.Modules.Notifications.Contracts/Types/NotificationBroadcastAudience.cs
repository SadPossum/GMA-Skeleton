namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(NotificationBroadcastAudienceJsonConverter))]
public enum NotificationBroadcastAudience
{
    Unknown = 0,
    TenantUsers = 1,
    TenantAdmins = 2,
    PlatformUsers = 3,
    PlatformAdmins = 4
}
