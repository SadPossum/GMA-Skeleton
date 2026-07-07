namespace Catalog.Application.Handlers;

using Catalog.Application.Commands;
using Catalog.Application.Ports;
using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Caching;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class DiscontinueCatalogItemCommandHandler(
    ICatalogItemRepository repository,
    ISystemClock clock,
    IIdGenerator idGenerator,
    ICacheInvalidationQueue cacheInvalidation)
    : ICommandHandler<DiscontinueCatalogItemCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DiscontinueCatalogItemCommand command, CancellationToken cancellationToken)
    {
        CatalogItem? item = await repository.GetAsync(command.ItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result.Failure<Unit>(CatalogDomainErrors.ItemNotFound);
        }

        Result result = item.Discontinue(idGenerator.NewId(), clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        cacheInvalidation.Remove(CatalogCache.Item(item.Id));
        cacheInvalidation.RemoveByTag(CatalogCache.ItemsTag());

        return Result.Success(Unit.Value);
    }
}
