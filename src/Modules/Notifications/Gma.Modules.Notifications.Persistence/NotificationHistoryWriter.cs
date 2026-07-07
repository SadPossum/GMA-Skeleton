namespace Gma.Modules.Notifications.Persistence;

using Microsoft.Extensions.Logging;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Notifications;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using DomainNotificationSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;
using FrameworkNotificationSeverity = Gma.Framework.Notifications.NotificationSeverity;

internal sealed class NotificationHistoryWriter(
    INotificationHistoryRepository repository,
    NotificationsDbContext dbContext,
    ISystemClock clock,
    ILogger<NotificationHistoryWriter> logger)
    : IUserNotificationHistoryWriter
{
    public async ValueTask SaveAsync(UserNotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await repository.ExistsAsync(message.Id, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        Result<DomainNotificationSeverity> severity = ToDomainSeverity(message.Severity);
        if (severity.IsFailure)
        {
            logger.LogWarning(
                "User notification {NotificationId} could not be converted to a history record. Error: {ErrorCode}.",
                message.Id,
                severity.Error.Code);
            return;
        }

        Result<UserNotification> notification = UserNotification.Create(
            message.Id,
            message.TenantId,
            message.UserId,
            message.Module,
            message.Name,
            message.Version,
            message.Title,
            message.Body,
            severity.Value,
            message.OccurredAtUtc,
            clock.UtcNow,
            message.Payload.GetRawText());

        if (notification.IsFailure)
        {
            logger.LogWarning(
                "User notification {NotificationId} could not be converted to a history record. Error: {ErrorCode}.",
                message.Id,
                notification.Error.Code);
            return;
        }

        await repository.AddAsync(notification.Value, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result<DomainNotificationSeverity> ToDomainSeverity(FrameworkNotificationSeverity severity) =>
        severity switch
        {
            FrameworkNotificationSeverity.Info => Result.Success(DomainNotificationSeverity.Info),
            FrameworkNotificationSeverity.Success => Result.Success(DomainNotificationSeverity.Success),
            FrameworkNotificationSeverity.Warning => Result.Success(DomainNotificationSeverity.Warning),
            FrameworkNotificationSeverity.Error => Result.Success(DomainNotificationSeverity.Error),
            _ => Result.Failure<DomainNotificationSeverity>(NotificationsDomainErrors.SeverityInvalid)
        };
}
