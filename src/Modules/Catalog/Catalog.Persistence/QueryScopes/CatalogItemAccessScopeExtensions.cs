namespace Catalog.Persistence.QueryScopes;

using Catalog.Domain.Visibility;
using Catalog.Domain.Aggregates;

internal static class CatalogItemAccessScopeExtensions
{
    public static IQueryable<CatalogItem> ApplyAvailableCatalogItemsScope(
        this IQueryable<CatalogItem> query,
        AvailableCatalogItemsScope scope)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);

        return query.Where(item =>
            item.Status == CatalogItemState.Active &&
            item.ScopeId == scope.ScopeId &&
            (!item.AvailableRegions.Any() ||
             item.AvailableRegions.Any(region => region.Region == scope.Region)));
    }
}
