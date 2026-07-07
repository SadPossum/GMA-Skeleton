namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;

public sealed record AdminNotificationBroadcastItem(
    Guid BroadcastId,
    string? TenantId,
    NotificationBroadcastAudience Audience,
    string Module,
    string Name,
    int Version,
    string Title,
    string? Body,
    NotificationSeverity Severity,
    long StreamSequence,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    JsonElement Payload);
