namespace Gma.Modules.Notifications.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;
using FrameworkNotificationNames = Gma.Framework.Notifications.NotificationNames;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record UserNotificationRequestedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "user-notification-requested";
    public const int EventVersion = 1;
    public const int SourceModuleMaxLength = 128;
    public const int NameMaxLength = 128;
    public const int TitleMaxLength = 256;
    public const int BodyMaxLength = 4096;
    public const int PayloadJsonMaxLength = 32768;

    public UserNotificationRequestedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        string userId,
        string sourceModule,
        string notificationName,
        int notificationVersion,
        string title,
        string? body,
        NotificationSeverity severity,
        string payloadJson)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.UserId = NotificationRecipientUserIds.Normalize(userId, nameof(userId));
        this.SourceModule = NormalizeSourceModule(sourceModule);
        this.NotificationName = NormalizeNotificationName(notificationName);
        this.NotificationVersion = notificationVersion > 0
            ? notificationVersion
            : throw new ArgumentOutOfRangeException(nameof(notificationVersion), notificationVersion, "Notification version must be positive.");
        this.Title = NormalizeText(title, TitleMaxLength, nameof(title));
        this.Body = string.IsNullOrWhiteSpace(body)
            ? null
            : NormalizeText(body, BodyMaxLength, nameof(body));
        this.Severity = NormalizeSeverity(severity);
        this.PayloadJson = NormalizePayloadJson(payloadJson);
    }

    public string UserId { get; }
    public string SourceModule { get; }
    public string NotificationName { get; }
    public int NotificationVersion { get; }
    public string Title { get; }
    public string? Body { get; }
    public NotificationSeverity Severity { get; }
    public string PayloadJson { get; }

    private static string NormalizeSourceModule(string sourceModule)
    {
        string normalized = SharedNameSegments.NormalizeKebabSegment(sourceModule, "source module", nameof(sourceModule));
        return normalized.Length <= SourceModuleMaxLength
            ? normalized
            : throw new ArgumentException($"sourceModule must be {SourceModuleMaxLength} characters or fewer.", nameof(sourceModule));
    }

    private static string NormalizeNotificationName(string notificationName)
    {
        string normalized = FrameworkNotificationNames.NormalizeName(notificationName, nameof(notificationName));
        return normalized.Length <= NameMaxLength
            ? normalized
            : throw new ArgumentException($"notificationName must be {NameMaxLength} characters or fewer.", nameof(notificationName));
    }

    private static string NormalizeText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();
        if (normalized.Length > maxLength ||
            normalized.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw new ArgumentException(
                $"{parameterName} must be {maxLength} characters or fewer and cannot contain control characters other than tab or line breaks.",
                parameterName);
        }

        return normalized;
    }

    private static NotificationSeverity NormalizeSeverity(NotificationSeverity severity) =>
        severity is not NotificationSeverity.Unknown && Enum.IsDefined(severity)
            ? severity
            : throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Notification severity must be a defined non-unknown value.");

    private static string NormalizePayloadJson(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        if (payloadJson.Length > PayloadJsonMaxLength)
        {
            throw new ArgumentException(
                $"payloadJson must be {PayloadJsonMaxLength} characters or fewer.",
                nameof(payloadJson));
        }

        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(payloadJson);
            string normalizedJson = System.Text.Json.JsonSerializer.Serialize(document.RootElement);
            return normalizedJson.Length <= PayloadJsonMaxLength
                ? normalizedJson
                : throw new ArgumentException(
                    $"payloadJson must be {PayloadJsonMaxLength} characters or fewer.",
                    nameof(payloadJson));
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new ArgumentException("payloadJson must be valid JSON.", nameof(payloadJson), exception);
        }
    }
}
