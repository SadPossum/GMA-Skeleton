namespace Gma.Modules.Notifications.Domain.Aggregates;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class UserNotification : TenantAggregateRoot<Guid>
{
    public const int UserIdMaxLength = 256;
    public const int ModuleMaxLength = 128;
    public const int NameMaxLength = 128;
    public const int TitleMaxLength = 256;
    public const int BodyMaxLength = 4096;
    public const int SeverityMaxLength = NotificationSeverityNames.MaxLength;

    private UserNotification() { }

    private UserNotification(Guid id, string tenantId)
        : base(id, tenantId)
    {
    }

    public NotificationRecipient Recipient { get; private set; }
    public NotificationSource Source { get; private set; } = null!;
    public NotificationContent Content { get; private set; } = null!;
    public NotificationSeverity Severity { get; private set; }
    public long StreamSequence { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public NotificationPayload Payload { get; private set; }

    public static Result<UserNotification> Create(
        Guid id,
        string tenantId,
        string userId,
        string module,
        string name,
        int version,
        string title,
        string? body,
        NotificationSeverity severity,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset createdAtUtc,
        string payloadJson)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<UserNotification>(NotificationsDomainErrors.NotificationIdRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            return Result.Failure<UserNotification>(NotificationsDomainErrors.TenantInvalid);
        }

        Result<NotificationRecipient> recipient = NotificationRecipient.Create(userId);
        if (recipient.IsFailure)
        {
            return Result.Failure<UserNotification>(recipient.Error);
        }

        Result<NotificationSource> source = NotificationSource.Create(module, name, version);
        if (source.IsFailure)
        {
            return Result.Failure<UserNotification>(source.Error);
        }

        Result<NotificationContent> content = NotificationContent.Create(title, body);
        if (content.IsFailure)
        {
            return Result.Failure<UserNotification>(content.Error);
        }

        if (!IsValidSeverity(severity))
        {
            return Result.Failure<UserNotification>(NotificationsDomainErrors.SeverityInvalid);
        }

        Result<NotificationPayload> payload = NotificationPayload.Create(payloadJson);
        if (payload.IsFailure)
        {
            return Result.Failure<UserNotification>(payload.Error);
        }

        UserNotification notification = new(id, normalizedTenantId!)
        {
            Recipient = recipient.Value,
            Source = source.Value,
            Content = content.Value,
            Severity = severity,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = createdAtUtc,
            Payload = payload.Value
        };

        return Result.Success(notification);
    }

    public bool MarkRead(DateTimeOffset readAtUtc)
    {
        if (this.ReadAtUtc is not null)
        {
            return false;
        }

        this.ReadAtUtc = readAtUtc;
        return true;
    }

    private static bool IsValidSeverity(NotificationSeverity severity) =>
        severity is
            NotificationSeverity.Info or
            NotificationSeverity.Success or
            NotificationSeverity.Warning or
            NotificationSeverity.Error;
}
