namespace Gma.Modules.Notifications.Application.Commands;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record MarkAllNotificationBroadcastsReadCommand(
    string? TenantId,
    NotificationBroadcastRecipientKind RecipientKind,
    string RecipientId) : ITransactionalCommand<MarkAllNotificationBroadcastsReadResponse>;
