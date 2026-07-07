namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Numerics;
using Gma.Framework.Results;

public readonly record struct CatalogPrice
{
    private CatalogPrice(decimal value) => this.Value = value;

    public decimal Value { get; }

    public static Result<CatalogPrice> Create(decimal price)
    {
        if (price <= 0)
        {
            return Result.Failure<CatalogPrice>(CatalogDomainErrors.PriceMustBePositive);
        }

        return DecimalPrecision.Fits(price, CatalogItem.PricePrecision, CatalogItem.PriceScale)
            ? Result.Success(new CatalogPrice(price))
            : Result.Failure<CatalogPrice>(CatalogDomainErrors.PriceNotSupported);
    }

    public override string ToString() => this.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
