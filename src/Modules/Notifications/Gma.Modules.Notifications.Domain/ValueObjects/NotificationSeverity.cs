namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public enum NotificationSeverity
{
    Unknown = 0,
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

public static class NotificationSeverityNames
{
    public const string Info = "info";
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Error = "error";
    public const int MaxLength = 16;

    public static Result<NotificationSeverity> Parse(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return Result.Failure<NotificationSeverity>(NotificationsDomainErrors.SeverityInvalid);
        }

        string normalized = severity.Trim().ToLowerInvariant();
        return normalized switch
        {
            Info => Result.Success(NotificationSeverity.Info),
            Success => Result.Success(NotificationSeverity.Success),
            Warning => Result.Success(NotificationSeverity.Warning),
            Error => Result.Success(NotificationSeverity.Error),
            _ => Result.Failure<NotificationSeverity>(NotificationsDomainErrors.SeverityInvalid)
        };
    }

    public static string ToWireName(NotificationSeverity severity) =>
        severity switch
        {
            NotificationSeverity.Info => Info,
            NotificationSeverity.Success => Success,
            NotificationSeverity.Warning => Warning,
            NotificationSeverity.Error => Error,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Notification severity is invalid.")
        };
}
