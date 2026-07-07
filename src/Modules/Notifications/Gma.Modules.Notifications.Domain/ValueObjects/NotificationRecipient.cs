namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct NotificationRecipient
{
    private NotificationRecipient(string userId) => this.UserId = userId;

    public string UserId { get; }

    public static Result<NotificationRecipient> Create(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<NotificationRecipient>(NotificationsDomainErrors.UserIdInvalid);
        }

        string normalized = userId.Trim();
        return normalized.Length <= UserNotification.UserIdMaxLength && !normalized.Any(char.IsControl)
            ? Result.Success(new NotificationRecipient(normalized))
            : Result.Failure<NotificationRecipient>(NotificationsDomainErrors.UserIdInvalid);
    }
}
