namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public enum NotificationBroadcastRecipientKind
{
    Unknown = 0,
    User = 1,
    Admin = 2
}

public static class NotificationBroadcastRecipientKindNames
{
    public const string User = "user";
    public const string Admin = "admin";
    public const int MaxLength = 16;

    public static Result<NotificationBroadcastRecipientKind> Parse(string? recipientKind)
    {
        string normalized = (recipientKind ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            User => Result.Success(NotificationBroadcastRecipientKind.User),
            Admin => Result.Success(NotificationBroadcastRecipientKind.Admin),
            _ => Result.Failure<NotificationBroadcastRecipientKind>(NotificationsDomainErrors.BroadcastRecipientKindInvalid)
        };
    }

    public static string ToWireName(NotificationBroadcastRecipientKind recipientKind) =>
        recipientKind switch
        {
            NotificationBroadcastRecipientKind.User => User,
            NotificationBroadcastRecipientKind.Admin => Admin,
            _ => throw new ArgumentOutOfRangeException(
                nameof(recipientKind),
                recipientKind,
                "Notification broadcast recipient kind is invalid.")
        };
}
