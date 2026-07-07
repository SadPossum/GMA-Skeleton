namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetNotificationBroadcastStreamCursorQuery(
    string? TenantId,
    NotificationBroadcastRecipientKind RecipientKind,
    string RecipientId) : IQuery<long>;
