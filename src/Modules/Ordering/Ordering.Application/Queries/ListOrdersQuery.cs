namespace Ordering.Application.Queries;

using Ordering.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record ListOrdersQuery(
    AccessSubject Subject,
    int Page = Gma.Framework.Pagination.PageRequest.DefaultPage,
    int PageSize = Gma.Framework.Pagination.PageRequest.DefaultPageSize)
    : IQuery<OrderListResponse>;
