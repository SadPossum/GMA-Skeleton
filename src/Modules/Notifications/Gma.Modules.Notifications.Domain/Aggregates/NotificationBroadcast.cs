namespace Gma.Modules.Notifications.Domain.Aggregates;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

[DisableTenantFilter("Broadcast visibility is evaluated by audience so platform records can appear in tenant-scoped feeds.")]
public sealed class NotificationBroadcast : Entity<Guid>
{
    public const int ModuleMaxLength = UserNotification.ModuleMaxLength;
    public const int NameMaxLength = UserNotification.NameMaxLength;
    public const int TitleMaxLength = UserNotification.TitleMaxLength;
    public const int BodyMaxLength = UserNotification.BodyMaxLength;
    public const int SeverityMaxLength = UserNotification.SeverityMaxLength;

    private NotificationBroadcast() { }

    private NotificationBroadcast(Guid id)
        : base(id)
    {
    }

    public string? TenantId { get; private set; }
    public NotificationBroadcastAudience Audience { get; private set; }
    public NotificationSource Source { get; private set; } = null!;
    public NotificationContent Content { get; private set; } = null!;
    public NotificationSeverity Severity { get; private set; }
    public long StreamSequence { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public NotificationPayload Payload { get; private set; }

    public static Result<NotificationBroadcast> Create(
        Guid id,
        string? tenantId,
        NotificationBroadcastAudience audience,
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
            return Result.Failure<NotificationBroadcast>(NotificationsDomainErrors.NotificationIdRequired);
        }

        if (!IsValidAudience(audience))
        {
            return Result.Failure<NotificationBroadcast>(NotificationsDomainErrors.BroadcastAudienceInvalid);
        }

        string? normalizedTenantId = null;
        if (NotificationBroadcastAudienceNames.IsTenantScoped(audience))
        {
            if (!TenantIds.TryNormalize(tenantId, out normalizedTenantId))
            {
                return Result.Failure<NotificationBroadcast>(NotificationsDomainErrors.TenantInvalid);
            }
        }
        else if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<NotificationBroadcast>(NotificationsDomainErrors.PlatformBroadcastTenantForbidden);
        }

        Result<NotificationSource> source = NotificationSource.Create(module, name, version);
        if (source.IsFailure)
        {
            return Result.Failure<NotificationBroadcast>(source.Error);
        }

        Result<NotificationContent> content = NotificationContent.Create(title, body);
        if (content.IsFailure)
        {
            return Result.Failure<NotificationBroadcast>(content.Error);
        }

        if (!IsValidSeverity(severity))
        {
            return Result.Failure<NotificationBroadcast>(NotificationsDomainErrors.SeverityInvalid);
        }

        Result<NotificationPayload> payload = NotificationPayload.Create(payloadJson);
        if (payload.IsFailure)
        {
            return Result.Failure<NotificationBroadcast>(payload.Error);
        }

        NotificationBroadcast broadcast = new(id)
        {
            TenantId = normalizedTenantId,
            Audience = audience,
            Source = source.Value,
            Content = content.Value,
            Severity = severity,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = createdAtUtc,
            Payload = payload.Value
        };

        return Result.Success(broadcast);
    }

    private static bool IsValidAudience(NotificationBroadcastAudience audience) =>
        audience is
            NotificationBroadcastAudience.TenantUsers or
            NotificationBroadcastAudience.TenantAdmins or
            NotificationBroadcastAudience.PlatformUsers or
            NotificationBroadcastAudience.PlatformAdmins;

    private static bool IsValidSeverity(NotificationSeverity severity) =>
        severity is
            NotificationSeverity.Info or
            NotificationSeverity.Success or
            NotificationSeverity.Warning or
            NotificationSeverity.Error;
}
