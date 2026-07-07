namespace Gma.Modules.Notifications.Application.Commands;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record MarkNotificationBroadcastReadCommand(
    Guid BroadcastId,
    string? TenantId,
    NotificationBroadcastRecipientKind RecipientKind,
    string RecipientId) : ITransactionalCommand<Unit>;
