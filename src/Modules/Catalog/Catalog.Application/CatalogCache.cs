namespace Catalog.Application;

using System.Globalization;
using Catalog.Contracts;
using Gma.Framework.Caching;

internal static class CatalogCache
{
    public static CacheKey Item(Guid itemId) =>
        CacheKey.Scoped(CatalogModuleMetadata.Name, CatalogModuleMetadata.ItemCacheEntry, itemId.ToString("N"));

    public static CacheKey AvailableItem(Guid itemId, string regionCode) =>
        CacheKey.Scoped(
            CatalogModuleMetadata.Name,
            CatalogModuleMetadata.ItemCacheEntry,
            "available",
            regionCode,
            itemId.ToString("N"));

    public static CacheKey Items(int page, int pageSize) =>
        CacheKey.Scoped(
            CatalogModuleMetadata.Name,
            CatalogModuleMetadata.ItemsCacheEntry,
            page.ToString(CultureInfo.InvariantCulture),
            pageSize.ToString(CultureInfo.InvariantCulture));

    public static CacheKey AvailableItems(string regionCode, int page, int pageSize) =>
        CacheKey.Scoped(
            CatalogModuleMetadata.Name,
            CatalogModuleMetadata.ItemsCacheEntry,
            "available",
            regionCode,
            page.ToString(CultureInfo.InvariantCulture),
            pageSize.ToString(CultureInfo.InvariantCulture));

    public static CacheTag ItemsTag() =>
        CacheTag.Scoped(CatalogModuleMetadata.Name, CatalogModuleMetadata.ItemsCacheTag);
}
