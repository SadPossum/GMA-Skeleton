namespace Catalog.Tests;

using Gma.Framework.Naming;
using System.Text.Json;
using Catalog.Contracts;
using Catalog.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogIntegrationEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid ItemId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Contract_limits_match_domain_limits()
    {
        Assert.Equal(CatalogItem.SkuMaxLength, CatalogContractLimits.SkuMaxLength);
        Assert.Equal(CatalogItem.NameMaxLength, CatalogContractLimits.NameMaxLength);
        Assert.Equal(CatalogItem.CurrencyLength, CatalogContractLimits.CurrencyLength);
        Assert.Equal(CatalogItem.PricePrecision, CatalogContractLimits.PricePrecision);
        Assert.Equal(CatalogItem.PriceScale, CatalogContractLimits.PriceScale);
    }

    [Fact]
    public void Catalog_subjects_support_default_and_configured_application_namespaces()
    {
        Assert.Equal("gma.catalog.item-created.v1", CatalogIntegrationSubjects.ItemCreated);
        Assert.Equal("acme-orders.catalog.item-created.v1", CatalogIntegrationSubjects.CreateItemCreated("acme-orders"));
        Assert.Equal("acme-orders.catalog.item-updated.v1", CatalogIntegrationSubjects.CreateItemUpdated("acme-orders"));
        Assert.Equal(
            "acme-orders.catalog.item-discontinued.v1",
            CatalogIntegrationSubjects.CreateItemDiscontinued("acme-orders"));
    }

    [Fact]
    public void Created_event_normalizes_metadata_and_item_snapshot()
    {
        CatalogItemCreatedIntegrationEvent integrationEvent = new(
            EventId,
            " tenant-a ",
            OccurredAtUtc,
            ItemId,
            " sku-1 ",
            " Item name ",
            10m,
            " usd ");

        Assert.Equal(EventId, integrationEvent.EventId);
        Assert.Equal("tenant-a", integrationEvent.TenantId);
        Assert.Equal(OccurredAtUtc, integrationEvent.OccurredAtUtc);
        Assert.Equal(ItemId, integrationEvent.ItemId);
        Assert.Equal("SKU-1", integrationEvent.Sku);
        Assert.Equal("Item name", integrationEvent.Name);
        Assert.Equal(10m, integrationEvent.Price);
        Assert.Equal("USD", integrationEvent.Currency);
    }

    [Fact]
    public void Created_event_round_trips_through_web_json()
    {
        CatalogItemCreatedIntegrationEvent integrationEvent = CreateCreatedEvent();

        string json = JsonSerializer.Serialize(integrationEvent, JsonOptions);
        CatalogItemCreatedIntegrationEvent? deserialized = JsonSerializer.Deserialize<CatalogItemCreatedIntegrationEvent>(
            json,
            JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(integrationEvent, deserialized);
    }

    [Fact]
    public void Catalog_item_status_json_uses_stable_string_names()
    {
        CatalogItemUpdatedIntegrationEvent integrationEvent = CreateUpdatedEvent(CatalogItemStatus.Discontinued);

        string json = JsonSerializer.Serialize(integrationEvent, JsonOptions);
        CatalogItemUpdatedIntegrationEvent? deserialized =
            JsonSerializer.Deserialize<CatalogItemUpdatedIntegrationEvent>(json, JsonOptions);

        Assert.Contains("\"status\":\"discontinued\"", json, StringComparison.Ordinal);
        Assert.NotNull(deserialized);
        Assert.Equal(CatalogItemStatus.Discontinued, deserialized.Status);
        Assert.Equal(
            CatalogItemStatus.Active,
            JsonSerializer.Deserialize<CatalogItemStatus>("\"Active\"", JsonOptions));
    }

    [Theory]
    [InlineData(CatalogItemStatus.Active, "active")]
    [InlineData(CatalogItemStatus.Discontinued, "discontinued")]
    public void Catalog_item_status_names_use_stable_wire_names(CatalogItemStatus status, string expected)
    {
        Assert.Equal(expected, CatalogItemStatusNames.ToWireName(status));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unknown\"")]
    [InlineData("\"archived\"")]
    public void Catalog_item_status_json_rejects_numeric_or_unknown_values(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CatalogItemStatus>(json, JsonOptions));
    }

    [Fact]
    public void Catalog_item_status_json_rejects_unknown_writes()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(CatalogItemStatus.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((CatalogItemStatus)999, JsonOptions));
    }

    [Fact]
    public void Updated_event_maps_undefined_status_to_unknown()
    {
        CatalogItemUpdatedIntegrationEvent integrationEvent = CreateUpdatedEvent(status: (CatalogItemStatus)999);

        Assert.Equal(CatalogItemStatus.Unknown, integrationEvent.Status);
    }

    [Fact]
    public void Catalog_events_reject_invalid_metadata_and_item_snapshot()
    {
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(tenantId: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(tenantId: new string('x', TenantIds.MaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(itemId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(sku: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(sku: new string('x', CatalogContractLimits.SkuMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(name: " "));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(name: new string('x', CatalogContractLimits.NameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(price: 0));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(price: 10.123m));
        Assert.Throws<ArgumentException>(() => CreateCreatedEvent(currency: "US"));
    }

    [Fact]
    public void Discontinued_event_normalizes_sku()
    {
        CatalogItemDiscontinuedIntegrationEvent integrationEvent = new(
            EventId,
            "tenant-a",
            OccurredAtUtc,
            ItemId,
            " sku-1 ");

        Assert.Equal("SKU-1", integrationEvent.Sku);
    }

    [Fact]
    public void Projection_export_normalizes_snapshot_data_and_maps_undefined_status_to_unknown()
    {
        CatalogItemProjectionExport export = new(
            " tenant-a ",
            ItemId,
            " sku-1 ",
            " Item name ",
            10m,
            " usd ",
            (CatalogItemStatus)999);

        Assert.Equal("tenant-a", export.TenantId);
        Assert.Equal(ItemId, export.ItemId);
        Assert.Equal("SKU-1", export.Sku);
        Assert.Equal("Item name", export.Name);
        Assert.Equal(10m, export.Price);
        Assert.Equal("USD", export.Currency);
        Assert.Equal(CatalogItemStatus.Unknown, export.Status);
    }

    [Fact]
    public void Projection_export_rejects_invalid_snapshot_data()
    {
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(tenantId: " "));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(tenantId: new string('x', TenantIds.MaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(itemId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(sku: " "));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(sku: new string('x', CatalogContractLimits.SkuMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(name: " "));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(name: new string('x', CatalogContractLimits.NameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(price: 0));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(price: 10.123m));
        Assert.Throws<ArgumentException>(() => CreateProjectionExport(currency: "US"));
    }

    private static CatalogItemCreatedIntegrationEvent CreateCreatedEvent(
        Guid? eventId = null,
        string tenantId = "tenant-a",
        DateTimeOffset? occurredAtUtc = null,
        Guid? itemId = null,
        string sku = "SKU-1",
        string name = "Item name",
        decimal price = 10m,
        string currency = "USD") =>
        new(
            eventId ?? EventId,
            tenantId,
            occurredAtUtc ?? OccurredAtUtc,
            itemId ?? ItemId,
            sku,
            name,
            price,
            currency);

    private static CatalogItemUpdatedIntegrationEvent CreateUpdatedEvent(
        CatalogItemStatus status = CatalogItemStatus.Active) =>
        new(
            EventId,
            "tenant-a",
            OccurredAtUtc,
            ItemId,
            "SKU-1",
            "Item name",
            10m,
            "USD",
            status);

    private static CatalogItemProjectionExport CreateProjectionExport(
        string tenantId = "tenant-a",
        Guid? itemId = null,
        string sku = "SKU-1",
        string name = "Item name",
        decimal price = 10m,
        string currency = "USD",
        CatalogItemStatus status = CatalogItemStatus.Active) =>
        new(
            tenantId,
            itemId ?? ItemId,
            sku,
            name,
            price,
            currency,
            status);
}
