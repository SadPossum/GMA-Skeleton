namespace Gma.Modules.Notifications.Application.Ports;

using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Framework.Pagination;

public interface INotificationBroadcastRepository
{
    Task AddAsync(NotificationBroadcast broadcast, CancellationToken cancellationToken);

    Task<NotificationBroadcastItem?> GetVisibleAsync(
        Guid broadcastId,
        NotificationBroadcastRecipientContext recipient,
        CancellationToken cancellationToken);

    Task<NotificationBroadcastListResponse> ListVisibleAsync(
        NotificationBroadcastRecipientContext recipient,
        bool unreadOnly,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task<long> GetCurrentStreamSequenceAsync(
        NotificationBroadcastRecipientContext recipient,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationBroadcastItem>> ListNewVisibleAsync(
        NotificationBroadcastRecipientContext recipient,
        long afterStreamSequence,
        int batchSize,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid broadcastId,
        NotificationBroadcastRecipientContext recipient,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    Task<int> MarkAllVisibleReadAsync(
        NotificationBroadcastRecipientContext recipient,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    Task<AdminNotificationBroadcastListResponse> ListTenantBroadcastsAsync(
        string tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task<AdminNotificationBroadcastListResponse> ListPlatformBroadcastsAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
