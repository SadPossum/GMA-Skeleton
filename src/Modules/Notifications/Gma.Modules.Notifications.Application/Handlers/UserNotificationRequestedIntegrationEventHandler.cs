namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;
using ContractNotificationSeverity = Gma.Modules.Notifications.Contracts.NotificationSeverity;
using DomainNotificationSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;

[IntegrationEventHandler("user-notification-request", RequiresExplicitProducerBinding = true)]
internal sealed class UserNotificationRequestedIntegrationEventHandler(
    INotificationHistoryRepository repository,
    ISystemClock clock)
    : IIntegrationEventHandler<UserNotificationRequestedIntegrationEvent>
{
    public async Task HandleAsync(
        UserNotificationRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(integrationEvent.EventId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        Gma.Framework.Results.Result<UserNotification> notification = UserNotification.Create(
            integrationEvent.EventId,
            integrationEvent.TenantId,
            integrationEvent.UserId,
            integrationEvent.SourceModule,
            integrationEvent.NotificationName,
            integrationEvent.NotificationVersion,
            integrationEvent.Title,
            integrationEvent.Body,
            ToDomainSeverity(integrationEvent.Severity),
            integrationEvent.OccurredAtUtc,
            clock.UtcNow,
            integrationEvent.PayloadJson);

        if (notification.IsFailure)
        {
            throw new InvalidOperationException(
                $"Notification request {integrationEvent.EventId} could not be projected: {notification.Error.Code}.");
        }

        await repository.AddAsync(notification.Value, cancellationToken).ConfigureAwait(false);
    }

    private static DomainNotificationSeverity ToDomainSeverity(ContractNotificationSeverity severity) =>
        severity switch
        {
            ContractNotificationSeverity.Info => DomainNotificationSeverity.Info,
            ContractNotificationSeverity.Success => DomainNotificationSeverity.Success,
            ContractNotificationSeverity.Warning => DomainNotificationSeverity.Warning,
            ContractNotificationSeverity.Error => DomainNotificationSeverity.Error,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Notification severity must be a defined non-unknown value.")
        };
}
