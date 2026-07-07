namespace Catalog.Application.Ports;

using Catalog.Contracts;
using Catalog.Domain.Visibility;
using Gma.Framework.Pagination;

public interface ICatalogItemReadRepository
{
    Task<CatalogItemDto?> GetAsync(Guid itemId, CancellationToken cancellationToken);
    Task<CatalogItemDto?> GetAvailableAsync(
        Guid itemId,
        AvailableCatalogItemsScope scope,
        CancellationToken cancellationToken);

    Task<CatalogItemListResponse> ListAsync(PageRequest pageRequest, CancellationToken cancellationToken);

    Task<CatalogItemListResponse> ListAvailableAsync(
        AvailableCatalogItemsScope scope,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
