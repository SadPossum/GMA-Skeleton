namespace Catalog.Tests;

using Gma.Framework.Naming;
using Catalog.Domain.Aggregates;
using Catalog.Domain.Events;
using Gma.Framework.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogDomainEventTests
{
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid ItemId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Created_event_normalizes_metadata_and_item_snapshot()
    {
        CatalogItemCreatedDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            ItemId,
            " tenant-a ",
            " sku-1 ",
            " Catalog item ",
            10m,
            " usd ",
            ["US", "EU"]);

        Assert.Equal(EventId, domainEvent.EventId);
        Assert.Equal(OccurredAtUtc, domainEvent.OccurredAtUtc);
        Assert.Equal(ItemId, domainEvent.ItemId);
        Assert.Equal("tenant-a", domainEvent.ScopeId);
        Assert.Equal("SKU-1", domainEvent.Sku);
        Assert.Equal("Catalog item", domainEvent.Name);
        Assert.Equal(10m, domainEvent.Price);
        Assert.Equal("USD", domainEvent.Currency);
        Assert.Equal(["US", "EU"], domainEvent.AvailableRegions);
    }

    [Fact]
    public void Created_event_rejects_invalid_metadata_and_item_snapshot()
    {
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(itemId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(scopeId: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(scopeId: new string('x', ScopeIds.MaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(sku: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(sku: new string('x', CatalogItem.SkuMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(name: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(name: new string('x', CatalogItem.NameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(price: 0));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(price: 10.123m));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(currency: "US"));
    }

    [Fact]
    public void Updated_event_maps_undefined_status_to_unknown()
    {
        CatalogItemUpdatedDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            ItemId,
            "tenant-a",
            "SKU-1",
            "Catalog item",
            10m,
            "USD",
            (CatalogItemState)999,
            ["US"]);

        Assert.Equal(CatalogItemState.Unknown, domainEvent.Status);
        Assert.Equal(["US"], domainEvent.AvailableRegions);
    }

    [Fact]
    public void Discontinued_event_normalizes_sku()
    {
        CatalogItemDiscontinuedDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            ItemId,
            "tenant-a",
            " sku-1 ");

        Assert.Equal("SKU-1", domainEvent.Sku);
    }

    private static CatalogItemCreatedDomainEvent CreateCreatedEvent(
        Guid? eventId = null,
        DateTimeOffset? occurredAtUtc = null,
        Guid? itemId = null,
        string scopeId = "tenant-a",
        string sku = "SKU-1",
        string name = "Catalog item",
        decimal price = 10m,
        string currency = "USD") =>
        new(
            eventId ?? EventId,
            occurredAtUtc ?? OccurredAtUtc,
            itemId ?? ItemId,
            scopeId,
            sku,
            name,
            price,
            currency,
            availableRegions: null);
}
