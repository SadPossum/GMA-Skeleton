namespace Catalog.Application.Handlers;

using Catalog.Contracts;
using Catalog.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class CatalogItemDiscontinuedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<CatalogItemDiscontinuedDomainEvent>
{
    public Task HandleAsync(CatalogItemDiscontinuedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(CatalogModuleMetadata.Name).EnqueueAsync(
            new CatalogItemDiscontinuedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.ItemId,
                domainEvent.Sku),
            cancellationToken);
}
