namespace Ordering.Application.Queries;

using Ordering.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record GetOrderQuery(Guid OrderId, AccessSubject Subject) : IQuery<OrderDto>;
