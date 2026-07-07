namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public enum NotificationBroadcastAudience
{
    Unknown = 0,
    TenantUsers = 1,
    TenantAdmins = 2,
    PlatformUsers = 3,
    PlatformAdmins = 4
}

public static class NotificationBroadcastAudienceNames
{
    public const string TenantUsers = "tenant-users";
    public const string TenantAdmins = "tenant-admins";
    public const string PlatformUsers = "platform-users";
    public const string PlatformAdmins = "platform-admins";
    public const int MaxLength = 32;

    public static bool IsTenantScoped(NotificationBroadcastAudience audience) =>
        audience is NotificationBroadcastAudience.TenantUsers or NotificationBroadcastAudience.TenantAdmins;

    public static bool IsPlatformScoped(NotificationBroadcastAudience audience) =>
        audience is NotificationBroadcastAudience.PlatformUsers or NotificationBroadcastAudience.PlatformAdmins;

    public static bool TargetsUsers(NotificationBroadcastAudience audience) =>
        audience is NotificationBroadcastAudience.TenantUsers or NotificationBroadcastAudience.PlatformUsers;

    public static bool TargetsAdmins(NotificationBroadcastAudience audience) =>
        audience is NotificationBroadcastAudience.TenantAdmins or NotificationBroadcastAudience.PlatformAdmins;

    public static Result<NotificationBroadcastAudience> Parse(string? audience)
    {
        string normalized = (audience ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            TenantUsers => Result.Success(NotificationBroadcastAudience.TenantUsers),
            TenantAdmins => Result.Success(NotificationBroadcastAudience.TenantAdmins),
            PlatformUsers => Result.Success(NotificationBroadcastAudience.PlatformUsers),
            PlatformAdmins => Result.Success(NotificationBroadcastAudience.PlatformAdmins),
            _ => Result.Failure<NotificationBroadcastAudience>(NotificationsDomainErrors.BroadcastAudienceInvalid)
        };
    }

    public static string ToWireName(NotificationBroadcastAudience audience) =>
        audience switch
        {
            NotificationBroadcastAudience.TenantUsers => TenantUsers,
            NotificationBroadcastAudience.TenantAdmins => TenantAdmins,
            NotificationBroadcastAudience.PlatformUsers => PlatformUsers,
            NotificationBroadcastAudience.PlatformAdmins => PlatformAdmins,
            _ => throw new ArgumentOutOfRangeException(
                nameof(audience),
                audience,
                "Notification broadcast audience is invalid.")
        };
}
