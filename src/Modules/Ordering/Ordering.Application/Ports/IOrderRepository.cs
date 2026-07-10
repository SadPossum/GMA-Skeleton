namespace Ordering.Application.Ports;

using Ordering.Domain.Aggregates;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> ListDistinctUserIdsByCatalogItemAsync(
        string scopeId,
        Guid catalogItemId,
        CancellationToken cancellationToken);
}
