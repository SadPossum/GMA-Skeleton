namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct CatalogItemName
{
    private CatalogItemName(string value) => this.Value = value;

    public string Value { get; }

    public static Result<CatalogItemName> Create(string? name)
    {
        string normalized = Normalize(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result.Failure<CatalogItemName>(CatalogDomainErrors.NameRequired);
        }

        return normalized.Length <= CatalogItem.NameMaxLength
            ? Result.Success(new CatalogItemName(normalized))
            : Result.Failure<CatalogItemName>(CatalogDomainErrors.NameTooLong);
    }

    public static string Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    public override string ToString() => this.Value;
}
