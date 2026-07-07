namespace Gma.Modules.Notifications.Application.Ports;

using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Framework.AccessControl;
using Gma.Framework.Pagination;

public interface INotificationHistoryRepository
{
    Task AddAsync(UserNotification notification, CancellationToken cancellationToken);

    Task<NotificationHistoryItem?> GetAsync(
        Guid notificationId,
        AccessSubject subject,
        CancellationToken cancellationToken);

    Task<AdminNotificationHistoryItem?> GetTenantAsync(
        Guid notificationId,
        CancellationToken cancellationToken);

    Task<NotificationHistoryListResponse> ListAsync(
        AccessSubject subject,
        bool unreadOnly,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task<AdminNotificationHistoryListResponse> ListTenantAsync(
        string? userId,
        bool unreadOnly,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task<long> GetCurrentStreamSequenceForUserAsync(
        AccessSubject subject,
        CancellationToken cancellationToken);

    Task<long> GetCurrentStreamSequenceForTenantAsync(
        string? userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationHistoryItem>> ListNewForUserAsync(
        AccessSubject subject,
        long afterStreamSequence,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminNotificationHistoryItem>> ListNewForTenantAsync(
        string? userId,
        long afterStreamSequence,
        int batchSize,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid notificationId,
        AccessSubject subject,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(
        AccessSubject subject,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken);
}
