namespace Catalog.Domain.Aggregates;

using Gma.Framework.Naming;
using Catalog.Domain.Entities;
using Catalog.Domain.Errors;
using Catalog.Domain.Events;
using Catalog.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class CatalogItem : ScopedAggregateRoot<Guid>
{
    public const int SkuMaxLength = 64;
    public const int NameMaxLength = 256;
    public const int CurrencyLength = 3;
    public const int PricePrecision = 18;
    public const int PriceScale = 2;
    public const int RegionCodeMaxLength = 32;
    public const int AvailableRegionMaxCount = 32;

    private readonly List<CatalogItemAvailableRegion> availableRegions = [];

    private CatalogItem() { }

    private CatalogItem(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public CatalogSku Sku { get; private set; }
    public CatalogItemName Name { get; private set; }
    public CatalogPrice Price { get; private set; }
    public CurrencyCode Currency { get; private set; }
    public CatalogItemState Status { get; private set; } = CatalogItemState.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DiscontinuedAtUtc { get; private set; }
    public IReadOnlyCollection<CatalogItemAvailableRegion> AvailableRegions => this.availableRegions.AsReadOnly();

    public static Result<CatalogItem> Create(
        Guid id,
        string scopeId,
        string sku,
        string name,
        decimal price,
        string currency,
        IEnumerable<string>? availableRegions,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<CatalogItem>(CatalogDomainErrors.ItemIdRequired);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<CatalogItem>(CatalogDomainErrors.DomainEventIdRequired);
        }

        Result<CatalogItemValues> values = CatalogItemValues.Create(scopeId, sku, name, price, currency);
        if (values.IsFailure)
        {
            return Result.Failure<CatalogItem>(values.Error);
        }

        CatalogItem item = new(id, values.Value.ScopeId)
        {
            Sku = values.Value.Sku,
            Name = values.Value.Name,
            Price = values.Value.Price,
            Currency = values.Value.Currency,
            CreatedAtUtc = nowUtc
        };

        Result availableRegionsResult = item.SetAvailableRegions(availableRegions);
        if (availableRegionsResult.IsFailure)
        {
            return Result.Failure<CatalogItem>(availableRegionsResult.Error);
        }

        item.RaiseDomainEvent(new CatalogItemCreatedDomainEvent(
            eventId,
            nowUtc,
            item.Id,
            item.ScopeId,
            item.Sku.Value,
            item.Name.Value,
            item.Price.Value,
            item.Currency.Value,
            item.GetAvailableRegionCodes()));

        return Result.Success(item);
    }

    public Result Update(
        string sku,
        string name,
        decimal price,
        string currency,
        IEnumerable<string>? availableRegions,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureKnownStatus();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(CatalogDomainErrors.DomainEventIdRequired);
        }

        Result<CatalogItemValues> values = CatalogItemValues.Create(this.ScopeId, sku, name, price, currency);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        this.Sku = values.Value.Sku;
        this.Name = values.Value.Name;
        this.Price = values.Value.Price;
        this.Currency = values.Value.Currency;
        this.UpdatedAtUtc = nowUtc;
        Result availableRegionsResult = this.SetAvailableRegions(availableRegions);
        if (availableRegionsResult.IsFailure)
        {
            return availableRegionsResult;
        }

        this.RaiseDomainEvent(new CatalogItemUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            this.Sku.Value,
            this.Name.Value,
            this.Price.Value,
            this.Currency.Value,
            this.Status,
            this.GetAvailableRegionCodes()));

        return Result.Success();
    }

    public Result Discontinue(Guid eventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureCanDiscontinue();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(CatalogDomainErrors.DomainEventIdRequired);
        }

        this.Status = CatalogItemState.Discontinued;
        this.DiscontinuedAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new CatalogItemDiscontinuedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            this.Sku.Value));

        return Result.Success();
    }

    public static string NormalizeSku(string? sku) =>
        CatalogSku.Normalize(sku);

    public bool IsAvailableInRegion(string regionCode)
    {
        Result<CatalogRegionCode> normalized = CatalogRegionCode.Create(regionCode);
        return normalized.IsSuccess && this.IsAvailableInRegion(normalized.Value);
    }

    private bool IsAvailableInRegion(CatalogRegionCode region) =>
        this.availableRegions.Count == 0 ||
        this.availableRegions.Any(availableRegion => availableRegion.Region == region);

    private Result SetAvailableRegions(IEnumerable<string>? regionCodes)
    {
        Result<IReadOnlyList<CatalogRegionCode>> normalizedRegions = NormalizeAvailableRegions(regionCodes);
        if (normalizedRegions.IsFailure)
        {
            return Result.Failure(normalizedRegions.Error);
        }

        this.availableRegions.Clear();
        foreach (CatalogRegionCode region in normalizedRegions.Value)
        {
            this.availableRegions.Add(CatalogItemAvailableRegion.Create(region));
        }

        return Result.Success();
    }

    private string[] GetAvailableRegionCodes() =>
        this.availableRegions
            .Select(region => region.Region.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static Result<IReadOnlyList<CatalogRegionCode>> NormalizeAvailableRegions(IEnumerable<string>? regionCodes)
    {
        if (regionCodes is null)
        {
            return Result.Success<IReadOnlyList<CatalogRegionCode>>([]);
        }

        List<CatalogRegionCode> normalized = [];
        foreach (string regionCode in regionCodes.Where(regionCode => !string.IsNullOrWhiteSpace(regionCode)))
        {
            Result<CatalogRegionCode> region = CatalogRegionCode.Create(regionCode);
            if (region.IsFailure)
            {
                return Result.Failure<IReadOnlyList<CatalogRegionCode>>(region.Error);
            }

            normalized.Add(region.Value);
        }

        CatalogRegionCode[] distinct = normalized
            .Distinct()
            .OrderBy(region => region.Value, StringComparer.Ordinal)
            .ToArray();

        return distinct.Length <= AvailableRegionMaxCount
            ? Result.Success<IReadOnlyList<CatalogRegionCode>>(distinct)
            : Result.Failure<IReadOnlyList<CatalogRegionCode>>(CatalogDomainErrors.AvailableRegionLimitExceeded);
    }

    private Result EnsureCanDiscontinue() =>
        this.Status switch
        {
            CatalogItemState.Active => Result.Success(),
            CatalogItemState.Discontinued => Result.Failure(CatalogDomainErrors.ItemAlreadyDiscontinued),
            _ => Result.Failure(CatalogDomainErrors.ItemStatusUnknown)
        };

    private Result EnsureKnownStatus() =>
        this.Status is CatalogItemState.Active or CatalogItemState.Discontinued
            ? Result.Success()
            : Result.Failure(CatalogDomainErrors.ItemStatusUnknown);

    private sealed record CatalogItemValues(
        string ScopeId,
        CatalogSku Sku,
        CatalogItemName Name,
        CatalogPrice Price,
        CurrencyCode Currency)
    {
        public static Result<CatalogItemValues> Create(
            string scopeId,
            string? sku,
            string? name,
            decimal price,
            string? currency)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
            {
                return Result.Failure<CatalogItemValues>(CatalogDomainErrors.TenantRequired);
            }

            if (!ScopeIds.TryNormalize(scopeId, out string? normalizedScopeId))
            {
                return Result.Failure<CatalogItemValues>(CatalogDomainErrors.TenantInvalid);
            }

            Result<CatalogSku> skuResult = CatalogSku.Create(sku);
            if (skuResult.IsFailure)
            {
                return Result.Failure<CatalogItemValues>(skuResult.Error);
            }

            Result<CatalogItemName> nameResult = CatalogItemName.Create(name);
            if (nameResult.IsFailure)
            {
                return Result.Failure<CatalogItemValues>(nameResult.Error);
            }

            Result<CatalogPrice> priceResult = CatalogPrice.Create(price);
            if (priceResult.IsFailure)
            {
                return Result.Failure<CatalogItemValues>(priceResult.Error);
            }

            Result<CurrencyCode> currencyResult = CurrencyCode.Create(currency);
            if (currencyResult.IsFailure)
            {
                return Result.Failure<CatalogItemValues>(currencyResult.Error);
            }

            return Result.Success(new CatalogItemValues(
                normalizedScopeId,
                skuResult.Value,
                nameResult.Value,
                priceResult.Value,
                currencyResult.Value));
        }
    }
}
