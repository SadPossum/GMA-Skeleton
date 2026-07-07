namespace Ordering.Domain.Visibility;

using Ordering.Domain.Errors;
using Ordering.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed record OrderViewer
{
    private OrderViewer(OrderUserId userId, string tenantId)
    {
        this.UserId = userId;
        this.TenantId = tenantId;
    }

    public OrderUserId UserId { get; }
    public string TenantId { get; }

    public static Result<OrderViewer> User(string? userId, string? tenantId)
    {
        Result<OrderUserId> userIdResult = OrderUserId.Create(userId);
        if (userIdResult.IsFailure)
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.TenantRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.TenantInvalid);
        }

        return Result.Success(new OrderViewer(userIdResult.Value, normalizedTenantId));
    }
}
