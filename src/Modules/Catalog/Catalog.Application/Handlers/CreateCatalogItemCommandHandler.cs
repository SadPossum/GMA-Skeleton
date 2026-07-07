namespace Catalog.Application.Handlers;

using Catalog.Application.Commands;
using Catalog.Application.Mapping;
using Catalog.Application.Ports;
using Catalog.Contracts;
using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Caching;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class CreateCatalogItemCommandHandler(
    ICatalogItemRepository repository,
    ITenantContext tenantContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    ICacheInvalidationQueue cacheInvalidation)
    : ICommandHandler<CreateCatalogItemCommand, CatalogItemDto>
{
    public async Task<Result<CatalogItemDto>> HandleAsync(CreateCatalogItemCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return Result.Failure<CatalogItemDto>(CatalogDomainErrors.TenantRequired);
        }

        Result<CatalogItem> itemResult = CatalogItem.Create(
            idGenerator.NewId(),
            tenantContext.TenantId,
            command.Sku,
            command.Name,
            command.Price,
            command.Currency,
            command.AvailableRegions,
            idGenerator.NewId(),
            clock.UtcNow);

        if (itemResult.IsFailure)
        {
            return Result.Failure<CatalogItemDto>(itemResult.Error);
        }

        CatalogItem item = itemResult.Value;
        if (await repository.SkuExistsAsync(item.Sku.Value, excludingItemId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<CatalogItemDto>(CatalogDomainErrors.SkuAlreadyExists);
        }

        await repository.AddAsync(item, cancellationToken).ConfigureAwait(false);
        cacheInvalidation.RemoveByTag(CatalogCache.ItemsTag());

        return Result.Success(CatalogItemMapper.ToDto(item));
    }
}
