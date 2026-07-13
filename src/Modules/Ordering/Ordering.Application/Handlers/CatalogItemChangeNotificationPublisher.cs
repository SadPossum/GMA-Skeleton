namespace Ordering.Application.Handlers;

using System.Text.Json;
using Catalog.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Notifications.Contracts;
using Ordering.Application.Ports;
using Ordering.Contracts;

internal sealed class CatalogItemChangeNotificationPublisher(
    IOrderRepository orderRepository,
    IOutboxWriterRegistry outboxWriters,
    IIdGenerator idGenerator,
    ISystemClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(
        string scopeId,
        Guid catalogItemId,
        string sku,
        string name,
        CatalogItemStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> userIds = await orderRepository
            .ListDistinctUserIdsByCatalogItemAsync(scopeId, catalogItemId, cancellationToken)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
        {
            return;
        }

        var outbox = outboxWriters.GetRequired(OrderingModuleMetadata.Name);
        foreach (string userId in userIds)
        {
            string payloadJson = JsonSerializer.Serialize(
                new OrderedCatalogItemChangedNotificationPayload(catalogItemId, sku, name, status, reason),
                JsonOptions);

            await outbox.EnqueueAsync(
                new UserNotificationRequestedIntegrationEventV2(
                    idGenerator.NewId(),
                    scopeId,
                    clock.UtcNow,
                    userId,
                    OrderingModuleMetadata.Name,
                    OrderingNotificationNames.CatalogItemChanged,
                    OrderingNotificationNames.CatalogItemChangedVersion,
                    "Ordered item changed",
                    $"Item {sku} in one of your orders changed.",
                    NotificationSeverity.Info,
                    payloadJson,
                    [
                        new NotificationTag("delivery:web", NotificationTagKind.Delivery),
                        new NotificationTag(
                            "domain:order-updates",
                            NotificationTagKind.Domain,
                            "Order updates",
                            "Changes to catalog items referenced by the recipient's orders.")
                    ]),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record OrderedCatalogItemChangedNotificationPayload(
        Guid CatalogItemId,
        string Sku,
        string Name,
        CatalogItemStatus Status,
        string Reason);
}
