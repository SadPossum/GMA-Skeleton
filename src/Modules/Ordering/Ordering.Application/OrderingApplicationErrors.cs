namespace Ordering.Application;

using Ordering.Domain.Errors;
using Gma.Framework.Results;

public static class OrderingApplicationErrors
{
    public static Error OrderNotFound => OrderingDomainErrors.OrderNotFound;
    public static Error CatalogItemUnknown => OrderingDomainErrors.CatalogItemUnknown;
    public static Error CatalogItemDiscontinued => OrderingDomainErrors.CatalogItemDiscontinued;
    public static Error CatalogItemUnavailableInRegion => OrderingDomainErrors.CatalogItemUnavailableInRegion;
    public static Error CatalogItemStatusUnknown => OrderingDomainErrors.CatalogItemStatusUnknown;
    public static Error AccessDenied => OrderingDomainErrors.AccessDenied;
}
