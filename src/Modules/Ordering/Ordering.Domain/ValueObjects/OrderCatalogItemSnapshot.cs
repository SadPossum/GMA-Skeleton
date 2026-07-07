namespace Ordering.Domain.ValueObjects;

using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct OrderCatalogItemSnapshot
{
    private OrderCatalogItemSnapshot(
        Guid catalogItemId,
        string sku,
        string name,
        OrderAmount unitPrice,
        string currency)
    {
        this.CatalogItemId = catalogItemId;
        this.Sku = sku;
        this.Name = name;
        this.UnitPrice = unitPrice;
        this.Currency = currency;
    }

    public Guid CatalogItemId { get; }
    public string Sku { get; }
    public string Name { get; }
    public OrderAmount UnitPrice { get; }
    public string Currency { get; }

    public static Result<OrderCatalogItemSnapshot> Create(
        Guid catalogItemId,
        string? catalogSku,
        string? catalogItemName,
        decimal unitPrice,
        string? currency)
    {
        if (catalogItemId == Guid.Empty)
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogItemRequired);
        }

        string normalizedSku = NormalizeSku(catalogSku);
        if (string.IsNullOrWhiteSpace(normalizedSku))
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogSkuRequired);
        }

        if (normalizedSku.Length > Order.CatalogSkuMaxLength)
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogSkuTooLong);
        }

        string normalizedName = NormalizeName(catalogItemName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogItemNameRequired);
        }

        if (normalizedName.Length > Order.CatalogItemNameMaxLength)
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogItemNameTooLong);
        }

        Result<OrderAmount> unitPriceResult = OrderAmount.Create(
            unitPrice,
            OrderingDomainErrors.CatalogItemPriceMustBePositive,
            OrderingDomainErrors.CatalogItemPriceNotSupported);
        if (unitPriceResult.IsFailure)
        {
            return Result.Failure<OrderCatalogItemSnapshot>(unitPriceResult.Error);
        }

        string normalizedCurrency = NormalizeCurrency(currency);
        if (normalizedCurrency.Length != Order.CurrencyLength)
        {
            return Result.Failure<OrderCatalogItemSnapshot>(OrderingDomainErrors.CatalogItemCurrencyInvalid);
        }

        return Result.Success(new OrderCatalogItemSnapshot(
            catalogItemId,
            normalizedSku,
            normalizedName,
            unitPriceResult.Value,
            normalizedCurrency));
    }

    public static string NormalizeSku(string? catalogSku) =>
        string.IsNullOrWhiteSpace(catalogSku) ? string.Empty : catalogSku.Trim().ToUpperInvariant();

    public static string NormalizeName(string? catalogItemName) =>
        string.IsNullOrWhiteSpace(catalogItemName) ? string.Empty : catalogItemName.Trim();

    public static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
}
