namespace Ordering.Application.Handlers;

using Catalog.Contracts;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(OrderingModuleMetadata.CatalogItemUpdatedProjectionHandlerName)]
internal sealed class CatalogItemUpdatedProjectionHandler(
    ICatalogItemProjectionRepository repository,
    CatalogItemChangeNotificationPublisher notifications)
    : IIntegrationEventHandler<CatalogItemUpdatedIntegrationEvent>
{
    public async Task HandleAsync(CatalogItemUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await repository.UpsertAsync(
            new CatalogItemProjectionWriteModel(
                integrationEvent.TenantId,
                integrationEvent.ItemId,
                integrationEvent.Sku,
                integrationEvent.Name,
                integrationEvent.Price,
                integrationEvent.Currency,
                integrationEvent.Status,
                integrationEvent.AvailableRegions),
            cancellationToken).ConfigureAwait(false);

        await notifications.PublishAsync(
            integrationEvent.TenantId,
            integrationEvent.ItemId,
            integrationEvent.Sku,
            integrationEvent.Name,
            integrationEvent.Status,
            reason: "updated",
            cancellationToken).ConfigureAwait(false);
    }
}
