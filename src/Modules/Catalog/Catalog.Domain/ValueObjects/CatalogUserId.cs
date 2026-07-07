namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct CatalogUserId
{
    public const int MaxLength = 256;

    private CatalogUserId(string value) => this.Value = value;

    public string Value { get; }

    public static Result<CatalogUserId> Create(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure<CatalogUserId>(CatalogDomainErrors.UserIdRequired);
        }

        string normalized = userId.Trim();
        if (normalized.Length > MaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return Result.Failure<CatalogUserId>(CatalogDomainErrors.UserIdInvalid);
        }

        return Result.Success(new CatalogUserId(normalized));
    }

    public static string Normalize(string? userId) => Create(userId).Value.Value;

    public override string ToString() => this.Value;
}
