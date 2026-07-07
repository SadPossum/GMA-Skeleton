namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct CatalogSku
{
    private CatalogSku(string value) => this.Value = value;

    public string Value { get; }

    public static Result<CatalogSku> Create(string? sku)
    {
        string normalized = Normalize(sku);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result.Failure<CatalogSku>(CatalogDomainErrors.SkuRequired);
        }

        return normalized.Length <= CatalogItem.SkuMaxLength
            ? Result.Success(new CatalogSku(normalized))
            : Result.Failure<CatalogSku>(CatalogDomainErrors.SkuTooLong);
    }

    public static string Normalize(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? string.Empty : sku.Trim().ToUpperInvariant();

    public override string ToString() => this.Value;
}
