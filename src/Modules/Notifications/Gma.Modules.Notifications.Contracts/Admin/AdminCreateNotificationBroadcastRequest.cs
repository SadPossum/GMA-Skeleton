namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;

public sealed record AdminCreateNotificationBroadcastRequest(
    NotificationBroadcastAudience Audience,
    string Name,
    int Version,
    string Title,
    string? Body,
    NotificationSeverity Severity,
    DateTimeOffset? OccurredAtUtc,
    JsonElement? Payload);
