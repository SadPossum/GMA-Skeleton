namespace Catalog.Domain.Errors;

using Gma.Framework.Results;

public static class CatalogDomainErrors
{
    public static readonly Error ItemIdRequired = new("Catalog.ItemIdRequired", "A catalog item id is required.");
    public static readonly Error DomainEventIdRequired = new("Catalog.DomainEventIdRequired", "A domain event id is required.");
    public static readonly Error TenantRequired = new("Catalog.TenantRequired", "A tenant id is required.");
    public static readonly Error TenantInvalid = new("Catalog.TenantInvalid", "The tenant id is not valid.");
    public static readonly Error UserIdRequired = new("Catalog.UserIdRequired", "A user id is required.");
    public static readonly Error UserIdInvalid = new("Catalog.UserIdInvalid", "The user id is not valid.");
    public static readonly Error SkuRequired = new("Catalog.SkuRequired", "A SKU is required.");
    public static readonly Error SkuTooLong = new("Catalog.SkuTooLong", "The SKU is too long.");
    public static readonly Error NameRequired = new("Catalog.NameRequired", "A name is required.");
    public static readonly Error NameTooLong = new("Catalog.NameTooLong", "The name is too long.");
    public static readonly Error PriceMustBePositive = new("Catalog.PriceMustBePositive", "The item price must be greater than zero.");
    public static readonly Error PriceNotSupported = new("Catalog.PriceNotSupported", "The item price must fit the configured decimal precision.");
    public static readonly Error CurrencyRequired = new("Catalog.CurrencyRequired", "A currency is required.");
    public static readonly Error CurrencyInvalid = new("Catalog.CurrencyInvalid", "Currency must be a three-letter code.");
    public static readonly Error RegionInvalid = new("Catalog.RegionInvalid", "The region code is not valid.");
    public static readonly Error AvailableRegionLimitExceeded = new("Catalog.AvailableRegionLimitExceeded", "The catalog item has too many available regions.");
    public static readonly Error AccessDenied = new("Catalog.AccessDenied", "Access to the catalog resource was denied.");
    public static readonly Error ItemNotFound = new("Catalog.ItemNotFound", "The catalog item was not found.");
    public static readonly Error SkuAlreadyExists = new("Catalog.SkuAlreadyExists", "A catalog item with the same SKU already exists.");
    public static readonly Error ItemStatusUnknown = new("Catalog.ItemStatusUnknown", "The catalog item status is unknown.");
    public static readonly Error ItemAlreadyDiscontinued = new("Catalog.ItemAlreadyDiscontinued", "The catalog item is already discontinued.");
}
