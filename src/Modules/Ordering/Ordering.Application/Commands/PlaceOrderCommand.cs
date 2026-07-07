namespace Ordering.Application.Commands;

using Ordering.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record PlaceOrderCommand(
    Guid CatalogItemId,
    int Quantity,
    AccessSubject Subject,
    string RegionCode)
    : ITransactionalCommand<OrderDto>;
