namespace Catalog.Tests;

using Gma.Framework.Naming;
using Catalog.Domain.Aggregates;
using Catalog.Domain.Errors;
using Catalog.Domain.Events;
using Gma.Framework.Domain;
using Gma.Framework.Numerics;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogItemAggregateTests
{
    [Fact]
    public void Create_normalizes_and_rejects_invalid_tenant_id()
    {
        Result<CatalogItem> normalized = CreateItem(" tenant-a ");
        Result<CatalogItem> missing = CreateItem(" ");
        Result<CatalogItem> invalid = CreateItem(new string('x', TenantIds.MaxLength + 1));

        Assert.True(normalized.IsSuccess);
        Assert.Equal("tenant-a", normalized.Value.TenantId);
        Assert.True(missing.IsFailure);
        Assert.Equal(CatalogDomainErrors.TenantRequired, missing.Error);
        Assert.True(invalid.IsFailure);
        Assert.Equal(CatalogDomainErrors.TenantInvalid, invalid.Error);
    }

    [Fact]
    public void Create_normalizes_item_text()
    {
        Result<CatalogItem> result = CreateItem(
            "tenant-a",
            " sku-1 ",
            " Catalog item ",
            " usd ");

        Assert.True(result.IsSuccess);
        Assert.Equal("SKU-1", result.Value.Sku.Value);
        Assert.Equal("Catalog item", result.Value.Name.Value);
        Assert.Equal("USD", result.Value.Currency.Value);
    }

    [Fact]
    public void Create_rejects_invalid_item_text_before_persistence()
    {
        Assert.Equal(CatalogDomainErrors.SkuRequired, CreateItem("tenant-a", null!, "Catalog item", "USD").Error);
        Assert.Equal(
            CatalogDomainErrors.SkuTooLong,
            CreateItem("tenant-a", new string('x', CatalogItem.SkuMaxLength + 1), "Catalog item", "USD").Error);
        Assert.Equal(CatalogDomainErrors.NameRequired, CreateItem("tenant-a", "SKU-1", " ", "USD").Error);
        Assert.Equal(
            CatalogDomainErrors.NameTooLong,
            CreateItem("tenant-a", "SKU-1", new string('x', CatalogItem.NameMaxLength + 1), "USD").Error);
        Assert.Equal(CatalogDomainErrors.CurrencyRequired, CreateItem("tenant-a", "SKU-1", "Catalog item", null!).Error);
        Assert.Equal(CatalogDomainErrors.CurrencyInvalid, CreateItem("tenant-a", "SKU-1", "Catalog item", "US").Error);
    }

    [Fact]
    public void Create_rejects_prices_that_do_not_fit_persistence_precision()
    {
        decimal maxPrice = DecimalPrecision.MaxValue(CatalogItem.PricePrecision, CatalogItem.PriceScale);

        Assert.True(CreateItem("tenant-a", price: maxPrice).IsSuccess);
        Assert.Equal(CatalogDomainErrors.PriceNotSupported, CreateItem("tenant-a", price: 10.123m).Error);
        Assert.Equal(CatalogDomainErrors.PriceNotSupported, CreateItem("tenant-a", price: maxPrice + 0.01m).Error);
    }

    [Fact]
    public void Create_rejects_empty_item_and_event_ids()
    {
        Assert.Equal(CatalogDomainErrors.ItemIdRequired, CreateItem("tenant-a", itemId: Guid.Empty).Error);
        Assert.Equal(CatalogDomainErrors.DomainEventIdRequired, CreateItem("tenant-a", eventId: Guid.Empty).Error);
    }

    [Fact]
    public void Create_normalizes_available_regions_and_treats_empty_list_as_all_regions()
    {
        Result<CatalogItem> regional = CreateItem("tenant-a", availableRegions: [" us ", "EU", "us"]);
        Result<CatalogItem> global = CreateItem("tenant-a", availableRegions: []);

        Assert.True(regional.IsSuccess);
        Assert.Equal(["EU", "US"], regional.Value.AvailableRegions.Select(region => region.Region.Value).ToArray());
        Assert.True(regional.Value.IsAvailableInRegion("us"));
        Assert.False(regional.Value.IsAvailableInRegion("ca"));
        CatalogItemCreatedDomainEvent domainEvent =
            Assert.IsType<CatalogItemCreatedDomainEvent>(Assert.Single(regional.Value.DomainEvents));
        Assert.Equal(["EU", "US"], domainEvent.AvailableRegions);

        Assert.True(global.IsSuccess);
        Assert.Empty(global.Value.AvailableRegions);
        Assert.True(global.Value.IsAvailableInRegion("ca"));
    }

    [Fact]
    public void Create_rejects_invalid_available_regions()
    {
        Assert.Equal(CatalogDomainErrors.RegionInvalid, CreateItem("tenant-a", availableRegions: ["-us"]).Error);
        Assert.Equal(
            CatalogDomainErrors.AvailableRegionLimitExceeded,
            CreateItem(
                "tenant-a",
                availableRegions: Enumerable.Range(1, CatalogItem.AvailableRegionMaxCount + 1)
                    .Select(index => $"R{index}")
                    .ToArray()).Error);
    }

    [Fact]
    public void Update_and_discontinue_reject_empty_event_id()
    {
        CatalogItem item = CreateItem("tenant-a").Value;

        Assert.Equal(
            CatalogDomainErrors.DomainEventIdRequired,
            item.Update(
                "SKU-2",
                "Updated item",
                12m,
                "USD",
                availableRegions: null,
                eventId: Guid.Empty,
                nowUtc: DateTimeOffset.UtcNow).Error);
        Assert.Equal(
            CatalogDomainErrors.DomainEventIdRequired,
            item.Discontinue(Guid.Empty, DateTimeOffset.UtcNow).Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Update_and_discontinue_reject_unknown_item_status(int statusValue)
    {
        CatalogItem item = CreateItem("tenant-a").Value;
        item.ClearDomainEvents();
        SetStatus(item, (CatalogItemState)statusValue);

        Result update = item.Update(
            "SKU-2",
            "Updated item",
            12m,
            "USD",
            availableRegions: null,
            eventId: Guid.NewGuid(),
            nowUtc: DateTimeOffset.UtcNow);
        Result discontinue = item.Discontinue(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(CatalogDomainErrors.ItemStatusUnknown, update.Error);
        Assert.Equal(CatalogDomainErrors.ItemStatusUnknown, discontinue.Error);
        Assert.Equal((CatalogItemState)statusValue, item.Status);
        Assert.Empty(item.DomainEvents);
    }

    private static Result<CatalogItem> CreateItem(
        string tenantId,
        string sku = "SKU-1",
        string name = "Catalog item",
        string currency = "USD",
        decimal price = 10m,
        IEnumerable<string>? availableRegions = null,
        Guid? itemId = null,
        Guid? eventId = null) =>
        CatalogItem.Create(
            itemId ?? Guid.NewGuid(),
            tenantId,
            sku,
            name,
            price,
            currency,
            availableRegions,
            eventId: eventId ?? Guid.NewGuid(),
            nowUtc: DateTimeOffset.UtcNow);

    private static void SetStatus(CatalogItem item, CatalogItemState status) =>
        typeof(CatalogItem)
            .GetProperty(nameof(CatalogItem.Status))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(item, [status]);
}
