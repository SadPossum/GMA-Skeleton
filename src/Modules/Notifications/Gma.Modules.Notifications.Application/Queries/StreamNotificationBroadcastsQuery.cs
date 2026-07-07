namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record StreamNotificationBroadcastsQuery(
    string? TenantId,
    NotificationBroadcastRecipientKind RecipientKind,
    string RecipientId,
    long AfterStreamSequence,
    int BatchSize) : IQuery<IReadOnlyList<NotificationBroadcastItem>>;
