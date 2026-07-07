namespace Catalog.Application;

using Catalog.Domain.Errors;
using Gma.Framework.Results;

public static class CatalogApplicationErrors
{
    public static readonly Error ItemNotFound = CatalogDomainErrors.ItemNotFound;
    public static readonly Error SkuAlreadyExists = CatalogDomainErrors.SkuAlreadyExists;
    public static readonly Error ItemStatusUnknown = CatalogDomainErrors.ItemStatusUnknown;
    public static readonly Error ItemAlreadyDiscontinued = CatalogDomainErrors.ItemAlreadyDiscontinued;
    public static readonly Error RegionInvalid = CatalogDomainErrors.RegionInvalid;
    public static readonly Error AccessDenied = CatalogDomainErrors.AccessDenied;
}
