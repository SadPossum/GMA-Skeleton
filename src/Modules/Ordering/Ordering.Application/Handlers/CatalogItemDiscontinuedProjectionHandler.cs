namespace Ordering.Application.Handlers;

using Catalog.Contracts;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(OrderingModuleMetadata.CatalogItemDiscontinuedProjectionHandlerName)]
internal sealed class CatalogItemDiscontinuedProjectionHandler(
    ICatalogItemProjectionRepository repository,
    CatalogItemChangeNotificationPublisher notifications)
    : IIntegrationEventHandler<CatalogItemDiscontinuedIntegrationEvent>
{
    public async Task HandleAsync(CatalogItemDiscontinuedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await repository.MarkDiscontinuedAsync(integrationEvent.ScopeId, integrationEvent.ItemId, cancellationToken)
            .ConfigureAwait(false);

        await notifications.PublishAsync(
            integrationEvent.ScopeId,
            integrationEvent.ItemId,
            integrationEvent.Sku,
            integrationEvent.Sku,
            CatalogItemStatus.Discontinued,
            reason: "discontinued",
            cancellationToken).ConfigureAwait(false);
    }
}
