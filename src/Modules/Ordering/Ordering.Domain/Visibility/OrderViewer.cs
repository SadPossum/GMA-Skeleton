namespace Ordering.Domain.Visibility;

using Ordering.Domain.Errors;
using Ordering.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed record OrderViewer
{
    private OrderViewer(OrderUserId userId, string scopeId)
    {
        this.UserId = userId;
        this.ScopeId = scopeId;
    }

    public OrderUserId UserId { get; }
    public string ScopeId { get; }

    public static Result<OrderViewer> User(string? userId, string? scopeId)
    {
        Result<OrderUserId> userIdResult = OrderUserId.Create(userId);
        if (userIdResult.IsFailure)
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.TenantRequired);
        }

        if (!ScopeIds.TryNormalize(scopeId, out string? normalizedScopeId))
        {
            return Result.Failure<OrderViewer>(OrderingDomainErrors.TenantInvalid);
        }

        return Result.Success(new OrderViewer(userIdResult.Value, normalizedScopeId));
    }
}
