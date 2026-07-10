namespace Ordering.Domain.Errors;

using Gma.Framework.Results;

public static class OrderingDomainErrors
{
    public static readonly Error OrderIdRequired = new("Ordering.OrderIdRequired", "An order id is required.");
    public static readonly Error UserIdRequired = new("Ordering.UserIdRequired", "A user id is required.");
    public static readonly Error UserIdInvalid = new("Ordering.UserIdInvalid", "The user id is not valid.");
    public static readonly Error TenantRequired = new("Ordering.TenantRequired", "A scope id is required.");
    public static readonly Error TenantInvalid = new("Ordering.TenantInvalid", "The scope id is not valid.");
    public static readonly Error RegionInvalid = new("Ordering.RegionInvalid", "The region code is not valid.");
    public static readonly Error CatalogItemRequired = new("Ordering.CatalogItemRequired", "A catalog item id is required.");
    public static readonly Error CatalogSkuRequired = new("Ordering.CatalogSkuRequired", "A catalog item SKU is required.");
    public static readonly Error CatalogSkuTooLong = new("Ordering.CatalogSkuTooLong", "The catalog item SKU is too long.");
    public static readonly Error CatalogItemNameRequired = new("Ordering.CatalogItemNameRequired", "A catalog item name is required.");
    public static readonly Error CatalogItemNameTooLong = new("Ordering.CatalogItemNameTooLong", "The catalog item name is too long.");
    public static readonly Error CatalogItemPriceMustBePositive = new("Ordering.CatalogItemPriceMustBePositive", "The catalog item price must be greater than zero.");
    public static readonly Error CatalogItemPriceNotSupported = new("Ordering.CatalogItemPriceNotSupported", "The catalog item price must fit the configured decimal precision.");
    public static readonly Error CatalogItemCurrencyInvalid = new("Ordering.CatalogItemCurrencyInvalid", "The catalog item currency must be a three-letter code.");
    public static readonly Error QuantityMustBePositive = new("Ordering.QuantityMustBePositive", "The order quantity must be greater than zero.");
    public static readonly Error OrderTotalNotSupported = new("Ordering.OrderTotalNotSupported", "The order total must fit the configured decimal precision.");
    public static readonly Error AccessDenied = new("Ordering.AccessDenied", "Access to the order resource was denied.");
    public static readonly Error OrderNotFound = new("Ordering.OrderNotFound", "The order was not found.");
    public static readonly Error CatalogItemUnknown = new("Ordering.CatalogItemUnknown", "The catalog item is unknown to Ordering.");
    public static readonly Error CatalogItemDiscontinued = new("Ordering.CatalogItemDiscontinued", "The catalog item is discontinued.");
    public static readonly Error CatalogItemUnavailableInRegion = new("Ordering.CatalogItemUnavailableInRegion", "The catalog item is not available in the requested region.");
    public static readonly Error CatalogItemStatusUnknown = new("Ordering.CatalogItemStatusUnknown", "The catalog item status is unknown to Ordering.");
}
