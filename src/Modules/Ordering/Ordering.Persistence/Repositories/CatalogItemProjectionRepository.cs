namespace Ordering.Persistence.Repositories;

using Catalog.Contracts;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Ports;
using Gma.Framework.Runtime.Identity;

internal sealed class CatalogItemProjectionRepository(OrderingDbContext dbContext, IIdGenerator idGenerator) : ICatalogItemProjectionRepository
{
    public async Task<CatalogItemProjectionSnapshot?> GetAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        await dbContext.CatalogItemProjections
            .AsNoTracking()
            .Where(item => item.CatalogItemId == catalogItemId)
            .Select(item => new CatalogItemProjectionSnapshot(
                item.CatalogItemId,
                item.Sku,
                item.Name,
                item.Price,
                item.Currency,
                item.Status,
                item.GetAvailableRegions()))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task UpsertAsync(CatalogItemProjectionWriteModel item, CancellationToken cancellationToken)
    {
        CatalogItemProjection? projection = await dbContext.CatalogItemProjections
            .FirstOrDefaultAsync(
                projection => projection.ScopeId == item.ScopeId && projection.CatalogItemId == item.CatalogItemId,
                cancellationToken)
            .ConfigureAwait(false);

        if (projection is null)
        {
            dbContext.CatalogItemProjections.Add(CatalogItemProjection.Create(
                idGenerator.NewId(),
                item.ScopeId,
                item.CatalogItemId,
                item.Sku,
                item.Name,
                item.Price,
                item.Currency,
                item.Status,
                item.AvailableRegions));
            return;
        }

        projection.Update(item.Sku, item.Name, item.Price, item.Currency, item.Status, item.AvailableRegions);
    }

    public async Task MarkDiscontinuedAsync(string scopeId, Guid catalogItemId, CancellationToken cancellationToken)
    {
        CatalogItemProjection? projection = await dbContext.CatalogItemProjections
            .FirstOrDefaultAsync(
                item => item.ScopeId == scopeId && item.CatalogItemId == catalogItemId,
                cancellationToken)
            .ConfigureAwait(false);

        if (projection is null)
        {
            dbContext.CatalogItemProjections.Add(CatalogItemProjection.CreateDiscontinuedPlaceholder(
                idGenerator.NewId(),
                scopeId,
                catalogItemId));
            return;
        }

        projection.MarkDiscontinued();
    }
}
