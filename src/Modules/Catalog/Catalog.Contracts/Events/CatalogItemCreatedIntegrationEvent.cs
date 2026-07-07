namespace Catalog.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record CatalogItemCreatedIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "item-created";
    public const int EventVersion = 1;

    public CatalogItemCreatedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid itemId,
        string sku,
        string name,
        decimal price,
        string currency,
        IReadOnlyCollection<string>? availableRegions = null)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ItemId = IntegrationEventContractGuards.RequireId(itemId, nameof(itemId));
        this.Sku = NormalizeSku(sku);
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(
            name,
            CatalogContractLimits.NameMaxLength,
            nameof(name));
        this.Price = IntegrationEventContractGuards.RequirePositiveDecimal(
            price,
            CatalogContractLimits.PricePrecision,
            CatalogContractLimits.PriceScale,
            nameof(price));
        this.Currency = NormalizeCurrency(currency);
        this.AvailableRegions = CatalogRegionCodes.NormalizeMany(availableRegions);
    }

    public Guid ItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public IReadOnlyCollection<string> AvailableRegions { get; }

    private static string NormalizeSku(string sku) =>
        IntegrationEventContractGuards
            .NormalizeRequiredText(sku, CatalogContractLimits.SkuMaxLength, nameof(sku))
            .ToUpperInvariant();

    private static string NormalizeCurrency(string currency)
    {
        string normalized = IntegrationEventContractGuards
            .NormalizeRequiredText(currency, CatalogContractLimits.CurrencyLength, nameof(currency))
            .ToUpperInvariant();

        return normalized.Length == CatalogContractLimits.CurrencyLength
            ? normalized
            : throw new ArgumentException("currency must be a three-letter code.", nameof(currency));
    }
}
