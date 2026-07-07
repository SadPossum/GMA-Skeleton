namespace Ordering.Domain.ValueObjects;

using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct OrderRegionCode
{
    private OrderRegionCode(string value) => this.Value = value;

    public string Value { get; }

    public static Result<OrderRegionCode> Create(string? regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return Result.Failure<OrderRegionCode>(OrderingDomainErrors.RegionInvalid);
        }

        string normalized = regionCode.Trim().ToUpperInvariant();
        if (normalized.Length > Order.RegionCodeMaxLength ||
            normalized.StartsWith('-') ||
            normalized.EndsWith('-') ||
            normalized.Any(character => !IsAllowedRegionCodeCharacter(character)))
        {
            return Result.Failure<OrderRegionCode>(OrderingDomainErrors.RegionInvalid);
        }

        return Result.Success(new OrderRegionCode(normalized));
    }

    public static string Normalize(string? regionCode) => Create(regionCode).Value.Value;

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
