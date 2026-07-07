namespace Ordering.Application.Handlers;

using Ordering.Application.Ports;
using Ordering.Application.Queries;
using Ordering.Contracts;
using Ordering.Domain.Visibility;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListOrdersQueryHandler(
    IOrderReadRepository repository)
    : IQueryHandler<ListOrdersQuery, OrderListResponse>
{
    public async Task<Result<OrderListResponse>> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        Result<UserOrdersScope> scopeResult = CreateUserOrdersScope(query);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<OrderListResponse>(scopeResult.Error);
        }

        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);
        OrderListResponse response = await repository.ListAsync(scopeResult.Value, pageRequest, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }

    private static Result<UserOrdersScope> CreateUserOrdersScope(ListOrdersQuery query)
    {
        Result<OrderViewer> viewerResult = OrderViewer.User(query.Subject.Id, query.Subject.TenantId);
        return viewerResult.IsFailure
            ? Result.Failure<UserOrdersScope>(viewerResult.Error)
            : OrderingVisibilityPolicy.CanViewOwnOrders(viewerResult.Value);
    }
}
