namespace Catalog.Application.Handlers;

using Catalog.Application.Ports;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Domain.Errors;
using Gma.Framework.Caching;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetCatalogItemQueryHandler(
    ICatalogItemReadRepository repository,
    IApplicationCache cache)
    : IQueryHandler<GetCatalogItemQuery, CatalogItemDto>
{
    public async Task<Result<CatalogItemDto>> HandleAsync(GetCatalogItemQuery query, CancellationToken cancellationToken)
    {
        CatalogItemDto? item = await cache.GetOrCreateAsync(
            CatalogCache.Item(query.ItemId),
            token => new ValueTask<CatalogItemDto?>(repository.GetAsync(query.ItemId, token)),
            tags: [CatalogCache.ItemsTag()],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return item is null
            ? Result.Failure<CatalogItemDto>(CatalogDomainErrors.ItemNotFound)
            : Result.Success(item);
    }
}
