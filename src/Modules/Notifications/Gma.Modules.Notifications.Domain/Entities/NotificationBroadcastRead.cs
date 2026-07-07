namespace Gma.Modules.Notifications.Domain.Entities;

using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

[GlobalEntity]
public sealed class NotificationBroadcastRead : Entity<Guid>
{
    public const int RecipientIdMaxLength = UserNotification.UserIdMaxLength;
    public const string GlobalRecipientScope = "global";
    public const int RecipientScopeMaxLength = TenantIds.MaxLength + 7;

    private const string TenantRecipientScopePrefix = "tenant:";

    private NotificationBroadcastRead() { }

    private NotificationBroadcastRead(Guid id)
        : base(id)
    {
    }

    public Guid BroadcastId { get; private set; }
    public string RecipientScope { get; private set; } = GlobalRecipientScope;
    public NotificationBroadcastRecipientKind RecipientKind { get; private set; }
    public NotificationRecipient Recipient { get; private set; }
    public DateTimeOffset ReadAtUtc { get; private set; }

    public static Result<NotificationBroadcastRead> Create(
        Guid id,
        Guid broadcastId,
        string? tenantId,
        NotificationBroadcastRecipientKind recipientKind,
        string recipientId,
        DateTimeOffset readAtUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<NotificationBroadcastRead>(NotificationsDomainErrors.NotificationIdRequired);
        }

        if (broadcastId == Guid.Empty)
        {
            return Result.Failure<NotificationBroadcastRead>(NotificationsDomainErrors.NotificationIdRequired);
        }

        Result<string> recipientScope = CreateRecipientScope(tenantId);
        if (recipientScope.IsFailure)
        {
            return Result.Failure<NotificationBroadcastRead>(recipientScope.Error);
        }

        if (!IsValidRecipientKind(recipientKind))
        {
            return Result.Failure<NotificationBroadcastRead>(NotificationsDomainErrors.BroadcastRecipientKindInvalid);
        }

        Result<NotificationRecipient> recipient = NotificationRecipient.Create(recipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<NotificationBroadcastRead>(recipient.Error);
        }

        NotificationBroadcastRead read = new(id)
        {
            BroadcastId = broadcastId,
            RecipientScope = recipientScope.Value,
            RecipientKind = recipientKind,
            Recipient = recipient.Value,
            ReadAtUtc = readAtUtc
        };

        return Result.Success(read);
    }

    public static Result<string> CreateRecipientScope(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Success(GlobalRecipientScope);
        }

        return TenantIds.TryNormalize(tenantId, out string? normalizedTenantId)
            ? Result.Success(TenantRecipientScopePrefix + normalizedTenantId)
            : Result.Failure<string>(NotificationsDomainErrors.TenantInvalid);
    }

    private static bool IsValidRecipientKind(NotificationBroadcastRecipientKind recipientKind) =>
        recipientKind is NotificationBroadcastRecipientKind.User or NotificationBroadcastRecipientKind.Admin;
}
