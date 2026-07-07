namespace Ordering.Domain.ValueObjects;

using Ordering.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct OrderQuantity
{
    private OrderQuantity(int value) => this.Value = value;

    public int Value { get; }

    public static Result<OrderQuantity> Create(int quantity) =>
        quantity > 0
            ? Result.Success(new OrderQuantity(quantity))
            : Result.Failure<OrderQuantity>(OrderingDomainErrors.QuantityMustBePositive);

    public override string ToString() => this.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
