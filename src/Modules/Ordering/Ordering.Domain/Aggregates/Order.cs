namespace Ordering.Domain.Aggregates;

using Gma.Framework.Naming;
using Ordering.Domain.Errors;
using Ordering.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class Order : ScopedAggregateRoot<Guid>
{
    public const int CatalogSkuMaxLength = 64;
    public const int CatalogItemNameMaxLength = 256;
    public const int CurrencyLength = 3;
    public const int UserIdMaxLength = 256;
    public const int RegionCodeMaxLength = 32;
    public const int AmountPrecision = 18;
    public const int AmountScale = 2;

    private Order() { }

    private Order(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    private Guid catalogItemId;
    private string userId = string.Empty;
    private string catalogSku = string.Empty;
    private string catalogItemName = string.Empty;
    private decimal unitPrice;
    private string currency = string.Empty;
    private string regionCode = string.Empty;

    public OrderCatalogItemSnapshot CatalogItem =>
        OrderCatalogItemSnapshot.Create(
            this.catalogItemId,
            this.catalogSku,
            this.catalogItemName,
            this.unitPrice,
            this.currency).Value;
    public OrderQuantity Quantity { get; private set; }
    public OrderAmount Total { get; private set; }
    public OrderState Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CatalogItemId => this.catalogItemId;
    public string UserId => this.userId;
    public string CatalogSku => this.catalogSku;
    public string CatalogItemName => this.catalogItemName;
    public decimal UnitPrice => this.unitPrice;
    public string Currency => this.currency;
    public string RegionCode => this.regionCode;

    public static Result<Order> Create(
        Guid id,
        string scopeId,
        string userId,
        Guid catalogItemId,
        string catalogSku,
        string catalogItemName,
        decimal unitPrice,
        string currency,
        string regionCode,
        int quantity,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Order>(OrderingDomainErrors.OrderIdRequired);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<Order>(OrderingDomainErrors.TenantRequired);
        }

        if (!ScopeIds.TryNormalize(scopeId, out string? normalizedScopeId))
        {
            return Result.Failure<Order>(OrderingDomainErrors.TenantInvalid);
        }

        Result<OrderQuantity> quantityResult = OrderQuantity.Create(quantity);
        if (quantityResult.IsFailure)
        {
            return Result.Failure<Order>(quantityResult.Error);
        }

        Result<OrderCatalogItemSnapshot> catalogSnapshot = OrderCatalogItemSnapshot.Create(
            catalogItemId,
            catalogSku,
            catalogItemName,
            unitPrice,
            currency);
        if (catalogSnapshot.IsFailure)
        {
            return Result.Failure<Order>(catalogSnapshot.Error);
        }

        Result<OrderUserId> userIdResult = OrderUserId.Create(userId);
        if (userIdResult.IsFailure)
        {
            return Result.Failure<Order>(userIdResult.Error);
        }

        Result<OrderRegionCode> regionCodeResult = OrderRegionCode.Create(regionCode);
        if (regionCodeResult.IsFailure)
        {
            return Result.Failure<Order>(regionCodeResult.Error);
        }

        Result<OrderAmount> total = CalculateTotal(catalogSnapshot.Value.UnitPrice, quantityResult.Value);
        if (total.IsFailure)
        {
            return Result.Failure<Order>(total.Error);
        }

        Order order = new(id, normalizedScopeId)
        {
            userId = userIdResult.Value.Value,
            catalogItemId = catalogSnapshot.Value.CatalogItemId,
            catalogSku = catalogSnapshot.Value.Sku,
            catalogItemName = catalogSnapshot.Value.Name,
            unitPrice = catalogSnapshot.Value.UnitPrice.Value,
            currency = catalogSnapshot.Value.Currency,
            regionCode = regionCodeResult.Value.Value,
            Quantity = quantityResult.Value,
            Total = total.Value,
            Status = OrderState.Submitted,
            CreatedAtUtc = nowUtc
        };

        return Result.Success(order);
    }

    public static Result ValidateCatalogSnapshot(
        Guid catalogItemId,
        string? catalogSku,
        string? catalogItemName,
        decimal unitPrice,
        string? currency)
    {
        Result<OrderCatalogItemSnapshot> snapshot = OrderCatalogItemSnapshot.Create(
            catalogItemId,
            catalogSku,
            catalogItemName,
            unitPrice,
            currency);

        return snapshot.IsSuccess ? Result.Success() : Result.Failure(snapshot.Error);
    }

    private static Result<OrderAmount> CalculateTotal(OrderAmount unitPrice, OrderQuantity quantity)
    {
        decimal total;
        try
        {
            total = unitPrice.Value * quantity.Value;
        }
        catch (OverflowException)
        {
            return Result.Failure<OrderAmount>(OrderingDomainErrors.OrderTotalNotSupported);
        }

        return OrderAmount.Create(
            total,
            OrderingDomainErrors.OrderTotalNotSupported,
            OrderingDomainErrors.OrderTotalNotSupported);
    }

    public static string NormalizeCatalogSku(string? catalogSku) =>
        OrderCatalogItemSnapshot.NormalizeSku(catalogSku);

    public static string NormalizeCatalogItemName(string? catalogItemName) =>
        OrderCatalogItemSnapshot.NormalizeName(catalogItemName);

    public static string NormalizeCurrency(string? currency) =>
        OrderCatalogItemSnapshot.NormalizeCurrency(currency);

    public static string NormalizeUserId(string? userId) =>
        OrderUserId.Normalize(userId);

    public static string NormalizeRegionCode(string? regionCode) =>
        OrderRegionCode.Normalize(regionCode);
}
