namespace Catalog.Domain.ValueObjects;

using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct CatalogRegionCode
{
    private CatalogRegionCode(string value) => this.Value = value;

    public string Value { get; }

    public static Result<CatalogRegionCode> Create(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return Result.Failure<CatalogRegionCode>(CatalogDomainErrors.RegionInvalid);
        }

        string normalized = regionCode.Trim().ToUpperInvariant();
        if (normalized.Length > CatalogItem.RegionCodeMaxLength ||
            normalized.StartsWith('-') ||
            normalized.EndsWith('-') ||
            normalized.Any(character => !IsAllowedRegionCodeCharacter(character)))
        {
            return Result.Failure<CatalogRegionCode>(CatalogDomainErrors.RegionInvalid);
        }

        return Result.Success(new CatalogRegionCode(normalized));
    }

    public static string Normalize(string? regionCode) =>
        Create(regionCode).Value.Value;

    public override string ToString() => this.Value;

    private static bool IsAllowedRegionCodeCharacter(char character) =>
        character switch
        {
            >= 'A' and <= 'Z' => true,
            >= '0' and <= '9' => true,
            '-' => true,
            _ => false
        };
}
