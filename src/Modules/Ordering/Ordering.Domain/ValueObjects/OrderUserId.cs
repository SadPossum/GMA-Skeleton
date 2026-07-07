namespace Ordering.Domain.ValueObjects;

using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct OrderUserId
{
    private OrderUserId(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrderUserId> Create(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<OrderUserId>(OrderingDomainErrors.UserIdRequired);
        }

        string normalized = userId.Trim();
        if (normalized.Length > Order.UserIdMaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return Result.Failure<OrderUserId>(OrderingDomainErrors.UserIdInvalid);
        }

        return Result.Success(new OrderUserId(normalized));
    }

    public static string Normalize(string? userId) => Create(userId).Value.Value;

    public override string ToString() => this.Value;
}
