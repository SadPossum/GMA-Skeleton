namespace Ordering.Tests;

using Ordering.Domain.Errors;
using Ordering.Domain.ValueObjects;
using Ordering.Domain.Visibility;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderingVisibilityPolicyTests
{
    [Fact]
    public void Can_view_own_orders_returns_user_scope()
    {
        OrderViewer viewer = OrderViewer.User(" user-1 ", " tenant-a ").Value;

        Result<UserOrdersScope> result = OrderingVisibilityPolicy.CanViewOwnOrders(viewer);

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.ScopeId);
        Assert.Equal(OrderUserId.Create("user-1").Value, result.Value.UserId);
    }

    [Theory]
    [InlineData(null, "tenant-a")]
    [InlineData("user 1", "tenant-a")]
    [InlineData("user-1", null)]
    [InlineData("user-1", "tenant a")]
    public void Viewer_rejects_invalid_inputs(string? userId, string? scopeId)
    {
        Result<OrderViewer> viewer = OrderViewer.User(userId, scopeId);

        Assert.True(viewer.IsFailure);
        Error[] expectedErrors =
        [
            OrderingDomainErrors.AccessDenied,
            OrderingDomainErrors.TenantRequired,
            OrderingDomainErrors.TenantInvalid
        ];
        Assert.True(expectedErrors.Contains(viewer.Error));
    }
}
