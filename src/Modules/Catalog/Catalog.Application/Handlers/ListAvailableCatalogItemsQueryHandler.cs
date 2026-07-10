namespace Catalog.Application.Handlers;

using Catalog.Application.Ports;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Domain.Visibility;
using Gma.Framework.Caching;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class ListAvailableCatalogItemsQueryHandler(
    ICatalogItemReadRepository repository,
    IApplicationCache cache,
    IScopeContext scopeContext)
    : IQueryHandler<ListAvailableCatalogItemsQuery, CatalogItemListResponse>
{
    public async Task<Result<CatalogItemListResponse>> HandleAsync(
        ListAvailableCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        Result<AvailableCatalogItemsScope> scopeResult = this.CreateAvailableItemsScope(query);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<CatalogItemListResponse>(scopeResult.Error);
        }

        AvailableCatalogItemsScope scope = scopeResult.Value;
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);

        CatalogItemListResponse response = await cache.GetOrCreateAsync(
            CatalogCache.AvailableItems(scope.Region.Value, pageRequest.Page, pageRequest.PageSize),
            token => new ValueTask<CatalogItemListResponse>(repository.ListAvailableAsync(scope, pageRequest, token)),
            tags: [CatalogCache.ItemsTag()],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Result.Success(response);
    }

    private Result<AvailableCatalogItemsScope> CreateAvailableItemsScope(ListAvailableCatalogItemsQuery query)
    {
        Result<CatalogViewer> viewerResult = CatalogViewer.User(
            query.Subject.Id,
            scopeContext.ScopeId,
            query.SubjectRegionCode);
        return viewerResult.IsFailure
            ? Result.Failure<AvailableCatalogItemsScope>(viewerResult.Error)
            : CatalogAvailabilityPolicy.CanViewAvailableItems(viewerResult.Value, query.RegionCode);
    }
}
