namespace Ordering.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Ordering.Application.Ports;
using Ordering.Contracts;
using Ordering.Domain.Aggregates;
using Ordering.Domain.Visibility;
using Ordering.Persistence.QueryScopes;
using Gma.Framework.Pagination;

internal sealed class OrderReadRepository(OrderingDbContext dbContext) : IOrderReadRepository
{
    public async Task<OrderDto?> GetAsync(Guid orderId, UserOrdersScope scope, CancellationToken cancellationToken)
    {
        Order? order = await dbContext.Orders
            .ApplyUserOrdersScope(scope)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        return order is null ? null : Map(order);
    }

    public async Task<OrderListResponse> ListAsync(
        UserOrdersScope scope,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Order> orders = dbContext.Orders
            .ApplyUserOrdersScope(scope)
            .AsNoTracking()
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.Id);

        int totalCount = await orders.CountAsync(cancellationToken).ConfigureAwait(false);
        Order[] items = await orders
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OrderListResponse(
            items.Select(Map).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            totalCount);
    }

    private static OrderDto Map(Order order) =>
        new(
            order.Id,
            order.UserId,
            order.CatalogItemId,
            order.CatalogSku,
            order.CatalogItemName,
            order.UnitPrice,
            order.Currency,
            order.RegionCode,
            order.Quantity.Value,
            order.Total.Value,
            MapStatus(order.Status),
            order.CreatedAtUtc);

    private static OrderStatus MapStatus(OrderState status) =>
        status switch
        {
            OrderState.Submitted => OrderStatus.Submitted,
            _ => OrderStatus.Unknown
        };
}
