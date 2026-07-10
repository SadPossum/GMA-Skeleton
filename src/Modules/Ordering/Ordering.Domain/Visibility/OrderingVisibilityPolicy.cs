namespace Ordering.Domain.Visibility;

using Gma.Framework.Results;

public static class OrderingVisibilityPolicy
{
    public static Result<UserOrdersScope> CanViewOwnOrders(OrderViewer viewer) =>
        Result.Success(new UserOrdersScope(viewer.ScopeId, viewer.UserId));
}
