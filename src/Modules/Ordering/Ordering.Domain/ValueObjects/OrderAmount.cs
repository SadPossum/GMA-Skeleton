namespace Ordering.Domain.ValueObjects;

using Ordering.Domain.Aggregates;
using Gma.Framework.Numerics;
using Gma.Framework.Results;

public readonly record struct OrderAmount
{
    private OrderAmount(decimal value) => this.Value = value;

    public decimal Value { get; }

    public static Result<OrderAmount> Create(decimal amount, Error nonPositiveError, Error precisionError)
    {
        if (amount <= 0)
        {
            return Result.Failure<OrderAmount>(nonPositiveError);
        }

        return DecimalPrecision.Fits(amount, Order.AmountPrecision, Order.AmountScale)
            ? Result.Success(new OrderAmount(amount))
            : Result.Failure<OrderAmount>(precisionError);
    }

    public override string ToString() => this.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
