namespace Gma.Modules.Notifications.Domain.ValueObjects;

using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public sealed record NotificationContent
{
    private NotificationContent()
    {
    }

    private NotificationContent(string title, string? body)
    {
        this.Title = title;
        this.Body = body;
    }

    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }

    public static Result<NotificationContent> Create(string? title, string? body)
    {
        string? normalizedTitle = NormalizeTextOrNull(title, UserNotification.TitleMaxLength, required: true);
        if (normalizedTitle is null)
        {
            return Result.Failure<NotificationContent>(NotificationsDomainErrors.TitleInvalid);
        }

        string? normalizedBody = NormalizeTextOrNull(body, UserNotification.BodyMaxLength, required: false);
        if (!string.IsNullOrWhiteSpace(body) && normalizedBody is null)
        {
            return Result.Failure<NotificationContent>(NotificationsDomainErrors.BodyInvalid);
        }

        return Result.Success(new NotificationContent(normalizedTitle, normalizedBody));
    }

    private static string? NormalizeTextOrNull(string? value, int maxLength, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? null : null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maxLength &&
               normalized.All(character => !char.IsControl(character) || character is '\r' or '\n' or '\t')
            ? normalized
            : null;
    }
}
