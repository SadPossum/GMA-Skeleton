namespace Catalog.Application.Handlers;

using Catalog.Application.Ports;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Domain.Visibility;
using Catalog.Domain.Errors;
using Gma.Framework.Caching;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetAvailableCatalogItemQueryHandler(
    ICatalogItemReadRepository repository,
    IApplicationCache cache)
    : IQueryHandler<GetAvailableCatalogItemQuery, CatalogItemDto>
{
    public async Task<Result<CatalogItemDto>> HandleAsync(
        GetAvailableCatalogItemQuery query,
        CancellationToken cancellationToken)
    {
        Result<AvailableCatalogItemsScope> scopeResult = CreateAvailableItemsScope(query);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<CatalogItemDto>(MapDeniedSingleResource(scopeResult.Error));
        }

        AvailableCatalogItemsScope scope = scopeResult.Value;
        CatalogItemDto? item = await cache.GetOrCreateAsync(
            CatalogCache.AvailableItem(query.ItemId, scope.Region.Value),
            token => new ValueTask<CatalogItemDto?>(repository.GetAvailableAsync(query.ItemId, scope, token)),
            tags: [CatalogCache.ItemsTag()],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return item is null
            ? Result.Failure<CatalogItemDto>(CatalogDomainErrors.ItemNotFound)
            : Result.Success(item);
    }

    private static Result<AvailableCatalogItemsScope> CreateAvailableItemsScope(GetAvailableCatalogItemQuery query)
    {
        Result<CatalogViewer> viewerResult = CatalogViewer.User(
            query.Subject.Id,
            query.Subject.TenantId,
            query.SubjectRegionCode);
        return viewerResult.IsFailure
            ? Result.Failure<AvailableCatalogItemsScope>(viewerResult.Error)
            : CatalogAvailabilityPolicy.CanViewAvailableItems(viewerResult.Value, query.RegionCode);
    }

    private static Error MapDeniedSingleResource(Error error) =>
        error == CatalogDomainErrors.RegionInvalid
            ? CatalogDomainErrors.RegionInvalid
            : CatalogDomainErrors.ItemNotFound;
}
