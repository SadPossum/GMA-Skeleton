namespace Gma.Modules.Notifications.Application.Commands;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record CreateNotificationBroadcastCommand(
    NotificationBroadcastAudience Audience,
    string? TenantId,
    string Module,
    string Name,
    int Version,
    string Title,
    string? Body,
    NotificationSeverity Severity,
    DateTimeOffset? OccurredAtUtc,
    string PayloadJson) : ITransactionalCommand<AdminCreateNotificationBroadcastResponse>;
