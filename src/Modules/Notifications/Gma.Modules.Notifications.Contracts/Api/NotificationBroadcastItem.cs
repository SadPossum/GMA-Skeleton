namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;

public sealed record NotificationBroadcastItem(
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
    DateTimeOffset? ReadAtUtc,
    JsonElement Payload);
