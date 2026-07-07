namespace Catalog.Domain.Events;

using Catalog.Domain.Aggregates;
using Gma.Framework.Domain;

public sealed record CatalogItemCreatedDomainEvent : TenantDomainEvent
{
    public CatalogItemCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid itemId,
        string tenantId,
        string sku,
        string name,
        decimal price,
        string currency,
        IReadOnlyCollection<string>? availableRegions)
        : base(eventId, occurredAtUtc, tenantId)
    {
        this.ItemId = DomainEventGuards.RequireId(itemId, nameof(itemId));
        this.Sku = NormalizeSku(sku);
        this.Name = DomainEventGuards.NormalizeRequiredText(name, CatalogItem.NameMaxLength, nameof(name));
        this.Price = DomainEventGuards.RequirePositiveDecimal(
            price,
            CatalogItem.PricePrecision,
            CatalogItem.PriceScale,
            nameof(price));
        this.Currency = NormalizeCurrency(currency);
        this.AvailableRegions = NormalizeAvailableRegions(availableRegions);
    }

    public Guid ItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public IReadOnlyCollection<string> AvailableRegions { get; }

    private static string NormalizeSku(string sku) =>
        DomainEventGuards
            .NormalizeRequiredText(sku, CatalogItem.SkuMaxLength, nameof(sku))
            .ToUpperInvariant();

    private static string NormalizeCurrency(string currency)
    {
        string normalized = DomainEventGuards
            .NormalizeRequiredText(currency, CatalogItem.CurrencyLength, nameof(currency))
            .ToUpperInvariant();

        return normalized.Length == CatalogItem.CurrencyLength
            ? normalized
            : throw new ArgumentException("currency must be a three-letter code.", nameof(currency));
    }

    private static string[] NormalizeAvailableRegions(IReadOnlyCollection<string>? regions) =>
        regions is null ? [] : regions.ToArray();
}
