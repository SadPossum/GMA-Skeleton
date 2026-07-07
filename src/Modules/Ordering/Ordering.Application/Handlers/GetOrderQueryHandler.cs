namespace Ordering.Application.Handlers;

using Ordering.Application.Ports;
using Ordering.Application.Queries;
using Ordering.Contracts;
using Ordering.Domain.Errors;
using Ordering.Domain.Visibility;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetOrderQueryHandler(
    IOrderReadRepository repository)
    : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        Result<UserOrdersScope> scopeResult = CreateUserOrdersScope(query);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<OrderDto>(MapDeniedSingleResource(scopeResult.Error));
        }

        OrderDto? order = await repository.GetAsync(query.OrderId, scopeResult.Value, cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? Result.Failure<OrderDto>(OrderingApplicationErrors.OrderNotFound)
            : Result.Success(order);
    }

    private static Result<UserOrdersScope> CreateUserOrdersScope(GetOrderQuery query)
    {
        Result<OrderViewer> viewerResult = OrderViewer.User(query.Subject.Id, query.Subject.TenantId);
        return viewerResult.IsFailure
            ? Result.Failure<UserOrdersScope>(viewerResult.Error)
            : OrderingVisibilityPolicy.CanViewOwnOrders(viewerResult.Value);
    }

    private static Error MapDeniedSingleResource(Error error) =>
        error == OrderingDomainErrors.TenantInvalid || error == OrderingDomainErrors.TenantRequired
            ? error
            : OrderingApplicationErrors.OrderNotFound;
}
