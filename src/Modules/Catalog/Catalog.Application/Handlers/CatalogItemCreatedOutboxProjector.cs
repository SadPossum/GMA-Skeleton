namespace Catalog.Application.Handlers;

using Catalog.Contracts;
using Catalog.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class CatalogItemCreatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<CatalogItemCreatedDomainEvent>
{
    public Task HandleAsync(CatalogItemCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(CatalogModuleMetadata.Name).EnqueueAsync(
            new CatalogItemCreatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.ItemId,
                domainEvent.Sku,
                domainEvent.Name,
                domainEvent.Price,
                domainEvent.Currency,
                domainEvent.AvailableRegions),
            cancellationToken);
}
