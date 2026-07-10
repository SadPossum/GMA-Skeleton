namespace Catalog.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record CatalogItemDiscontinuedIntegrationEvent : ScopedIntegrationEvent
{
    public const string EventType = "item-discontinued";
    public const int EventVersion = 1;

    public CatalogItemDiscontinuedIntegrationEvent(
        Guid eventId,
        string scopeId,
        DateTimeOffset occurredAtUtc,
        Guid itemId,
        string sku)
        : base(eventId, scopeId, occurredAtUtc, EventType, EventVersion)
    {
        this.ItemId = IntegrationEventContractGuards.RequireId(itemId, nameof(itemId));
        this.Sku = IntegrationEventContractGuards
            .NormalizeRequiredText(sku, CatalogContractLimits.SkuMaxLength, nameof(sku))
            .ToUpperInvariant();
    }

    public Guid ItemId { get; }
    public string Sku { get; }
}
