namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct CurrencyCode
{
    private CurrencyCode(string value) => this.Value = value;

    public string Value { get; }

    public static Result<CurrencyCode> Create(string? currency)
    {
        string normalized = Normalize(currency);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result.Failure<CurrencyCode>(CatalogDomainErrors.CurrencyRequired);
        }

        return normalized.Length == CatalogItem.CurrencyLength
            ? Result.Success(new CurrencyCode(normalized))
            : Result.Failure<CurrencyCode>(CatalogDomainErrors.CurrencyInvalid);
    }

    public static string Normalize(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();

    public override string ToString() => this.Value;
}
