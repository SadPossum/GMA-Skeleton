namespace Gma.Modules.Notifications.Domain.Errors;

using Gma.Framework.Results;

public static class NotificationsDomainErrors
{
    public static readonly Error NotificationIdRequired = new("Notifications.NotificationIdRequired", "Notification id is required.");
    public static readonly Error TenantInvalid = new("Notifications.TenantInvalid", "Notification tenant id is invalid.");
    public static readonly Error UserIdInvalid = new("Notifications.UserIdInvalid", "Notification user id is invalid.");
    public static readonly Error ModuleInvalid = new("Notifications.ModuleInvalid", "Notification module is invalid.");
    public static readonly Error NameInvalid = new("Notifications.NameInvalid", "Notification name is invalid.");
    public static readonly Error VersionInvalid = new("Notifications.VersionInvalid", "Notification version is invalid.");
    public static readonly Error TitleInvalid = new("Notifications.TitleInvalid", "Notification title is invalid.");
    public static readonly Error BodyInvalid = new("Notifications.BodyInvalid", "Notification body is invalid.");
    public static readonly Error SeverityInvalid = new("Notifications.SeverityInvalid", "Notification severity is invalid.");
    public static readonly Error PayloadInvalid = new("Notifications.PayloadInvalid", "Notification payload JSON is invalid.");
    public static readonly Error NotificationNotFound = new("Notifications.NotificationNotFound", "Notification was not found.");
    public static readonly Error BroadcastAudienceInvalid = new("Notifications.BroadcastAudienceInvalid", "Notification broadcast audience is invalid.");
    public static readonly Error BroadcastRecipientKindInvalid = new("Notifications.BroadcastRecipientKindInvalid", "Notification broadcast recipient kind is invalid.");
    public static readonly Error PlatformBroadcastTenantForbidden = new("Notifications.PlatformBroadcastTenantForbidden", "Platform notification broadcasts cannot be tenant-scoped.");
    public static readonly Error BroadcastNotFound = new("Notifications.BroadcastNotFound", "Notification broadcast was not found.");
}
