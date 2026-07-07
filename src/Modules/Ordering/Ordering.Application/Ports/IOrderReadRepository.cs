namespace Ordering.Application.Ports;

using Ordering.Contracts;
using Ordering.Domain.Visibility;
using Gma.Framework.Pagination;

public interface IOrderReadRepository
{
    Task<OrderDto?> GetAsync(Guid orderId, UserOrdersScope scope, CancellationToken cancellationToken);

    Task<OrderListResponse> ListAsync(
        UserOrdersScope scope,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
