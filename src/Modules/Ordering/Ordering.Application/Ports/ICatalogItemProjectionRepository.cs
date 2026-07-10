namespace Ordering.Application.Ports;

public interface ICatalogItemProjectionRepository
{
    Task<CatalogItemProjectionSnapshot?> GetAsync(Guid catalogItemId, CancellationToken cancellationToken);
    Task UpsertAsync(CatalogItemProjectionWriteModel item, CancellationToken cancellationToken);
    Task MarkDiscontinuedAsync(string scopeId, Guid catalogItemId, CancellationToken cancellationToken);
}
