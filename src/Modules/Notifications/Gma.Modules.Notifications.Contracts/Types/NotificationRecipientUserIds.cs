namespace Gma.Modules.Notifications.Contracts;

public static class NotificationRecipientUserIds
{
    public const int MaxLength = 256;

    public static bool TryNormalize(string? userId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        string candidate = userId.Trim();
        if (candidate.Length > MaxLength ||
            candidate.Any(char.IsControl))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string Normalize(string userId, string parameterName = "userId") =>
        TryNormalize(userId, out string normalized)
            ? normalized
            : throw new ArgumentException(
                $"Notification recipient user id is required, must be {MaxLength} characters or fewer, and cannot contain control characters.",
                parameterName);
}
