namespace Catalog.Domain.Events;

using Catalog.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record CatalogItemDiscontinuedDomainEvent : TenantDomainEvent
{
    public CatalogItemDiscontinuedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid itemId,
        string tenantId,
        string sku)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.ItemId = DomainEventGuards.RequireId(itemId, nameof(itemId));
        this.Sku = DomainEventGuards
            .NormalizeRequiredText(sku, CatalogItem.SkuMaxLength, nameof(sku))
            .ToUpperInvariant();
    }

    public Guid ItemId { get; }
    public string Sku { get; }
}
