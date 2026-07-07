namespace Ordering.Application.Handlers;

using Catalog.Contracts;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(OrderingModuleMetadata.CatalogItemCreatedProjectionHandlerName)]
internal sealed class CatalogItemCreatedProjectionHandler(ICatalogItemProjectionRepository repository)
    : IIntegrationEventHandler<CatalogItemCreatedIntegrationEvent>
{
    public async Task HandleAsync(CatalogItemCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await repository.UpsertAsync(
            new CatalogItemProjectionWriteModel(
                integrationEvent.TenantId,
                integrationEvent.ItemId,
                integrationEvent.Sku,
                integrationEvent.Name,
                integrationEvent.Price,
                integrationEvent.Currency,
                CatalogItemStatus.Active,
                integrationEvent.AvailableRegions),
            cancellationToken).ConfigureAwait(false);
    }
}
