namespace Ordering.Persistence.Repositories;

using Catalog.Contracts;
using Ordering.Application.Ports;
using Gma.Framework.ProjectionRebuild;

internal sealed class CatalogItemProjectionRebuildWriter(
    ICatalogItemProjectionRepository repository,
    OrderingDbContext dbContext)
    : IProjectionRebuildWriter<CatalogItemProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<CatalogItemProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);

        foreach (CatalogItemProjectionExport snapshot in snapshots)
        {
            CatalogItemProjectionWriteModel writeModel = new(
                snapshot.TenantId,
                snapshot.ItemId,
                snapshot.Sku,
                snapshot.Name,
                snapshot.Price,
                snapshot.Currency,
                snapshot.Status,
                snapshot.AvailableRegions);

            if (!request.DryRun)
            {
                await repository.UpsertAsync(writeModel, cancellationToken).ConfigureAwait(false);
            }
        }

        if (request.DryRun)
        {
            return new ProjectionWriteResult(writtenCount: 0, skippedCount: snapshots.Count);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
