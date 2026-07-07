namespace Catalog.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;

public sealed record CatalogItemProjectionExport
{
    public CatalogItemProjectionExport(
        string tenantId,
        Guid itemId,
        string sku,
        string name,
        decimal price,
        string currency,
        CatalogItemStatus status,
        IReadOnlyCollection<string>? availableRegions = null)
    {
        this.TenantId = TenantIds.Normalize(tenantId);
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
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
        this.AvailableRegions = CatalogRegionCodes.NormalizeMany(availableRegions);
    }

    public string TenantId { get; }
    public Guid ItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public string Currency { get; }
    public CatalogItemStatus Status { get; }
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
