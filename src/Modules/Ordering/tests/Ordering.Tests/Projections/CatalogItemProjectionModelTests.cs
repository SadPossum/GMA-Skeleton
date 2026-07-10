namespace Ordering.Tests;

using Gma.Framework.Naming;
using Catalog.Contracts;
using Ordering.Application.Ports;
using Ordering.Domain.Aggregates;
using Ordering.Domain.Errors;
using Ordering.Persistence;
using Gma.Framework.ProjectionRebuild;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogItemProjectionModelTests
{
    private static readonly Guid ItemId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid RunId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Write_model_normalizes_tenant_and_catalog_snapshot_data()
    {
        CatalogItemProjectionWriteModel model = new(
            " tenant-a ",
            ItemId,
            " sku-1 ",
            " Projection item ",
            10m,
            " usd ",
            CatalogItemStatus.Active);

        Assert.Equal("tenant-a", model.ScopeId);
        Assert.Equal(ItemId, model.CatalogItemId);
        Assert.Equal("SKU-1", model.Sku);
        Assert.Equal("Projection item", model.Name);
        Assert.Equal(10m, model.Price);
        Assert.Equal("USD", model.Currency);
        Assert.Equal(CatalogItemStatus.Active, model.Status);
    }

    [Fact]
    public void Write_model_rejects_invalid_tenant_and_catalog_snapshot_data()
    {
        Assert.Throws<ArgumentException>(() => CreateWriteModel(scopeId: " "));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(scopeId: new string('x', ScopeIds.MaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(catalogItemId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(sku: " "));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(sku: new string('x', Order.CatalogSkuMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(name: " "));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(name: new string('x', Order.CatalogItemNameMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(price: 0));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(price: 10.123m));
        Assert.Throws<ArgumentException>(() => CreateWriteModel(currency: "US"));
    }

    [Fact]
    public void Write_model_maps_undefined_status_to_unknown()
    {
        CatalogItemProjectionWriteModel model = CreateWriteModel(status: (CatalogItemStatus)999);

        Assert.Equal(CatalogItemStatus.Unknown, model.Status);
    }

    [Fact]
    public void Snapshot_normalizes_observed_data_but_preserves_invalid_business_state_for_callers()
    {
        CatalogItemProjectionSnapshot snapshot = new(
            ItemId,
            " sku-1 ",
            " Projection item ",
            0,
            " usd ",
            (CatalogItemStatus)999);

        Assert.Equal("SKU-1", snapshot.Sku);
        Assert.Equal("Projection item", snapshot.Name);
        Assert.Equal(0, snapshot.Price);
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(CatalogItemStatus.Unknown, snapshot.Status);
    }

    [Fact]
    public void Snapshot_rejects_missing_catalog_item_id()
    {
        Assert.Throws<ArgumentException>(() => new CatalogItemProjectionSnapshot(
            Guid.Empty,
            "SKU-1",
            "Projection item",
            10m,
            "USD",
            CatalogItemStatus.Active));
    }

    [Fact]
    public void Persistence_projection_maps_undefined_status_to_unknown()
    {
        CatalogItemProjection projection = CatalogItemProjection.Create(
            Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            "tenant-a",
            ItemId,
            "SKU-1",
            "Projection item",
            10m,
            "USD",
            (CatalogItemStatus)999);

        Assert.Equal(CatalogItemStatus.Unknown, projection.Status);
    }

    [Fact]
    public void Projection_rebuild_checkpoint_round_trips_state_and_requires_tenant_scope()
    {
        ProjectionRebuildCheckpointKey key = new(
            " ordering ",
            RunId,
            " Catalog-Item-Projections ",
            " tenant-a ");
        ProjectionRebuildCheckpoint checkpoint = new(
            " sku-1 ",
            processedCount: 3,
            writtenCount: 2,
            skippedCount: 1,
            failedCount: 0,
            projectionVersion: 1,
            updatedAtUtc: Now);

        OrderingProjectionRebuildCheckpoint state = OrderingProjectionRebuildCheckpoint.Create(key, checkpoint);
        ProjectionRebuildCheckpoint roundTripped = state.ToCheckpoint();

        Assert.Equal(RunId, state.RunId);
        Assert.Equal("tenant-a", state.ScopeId);
        Assert.Equal("catalog-item-projections", state.ProjectionName);
        Assert.Equal("sku-1", state.Cursor);
        Assert.Equal(checkpoint, roundTripped);

        ProjectionRebuildCheckpoint completed = checkpoint.Complete(Now.AddSeconds(1));
        state.Update(completed);

        Assert.Equal(completed, state.ToCheckpoint());
        Assert.Throws<InvalidOperationException>(() => OrderingProjectionRebuildCheckpoint.Create(
            new ProjectionRebuildCheckpointKey("ordering", RunId, "catalog-item-projections", scopeId: null),
            checkpoint));
        Assert.Throws<ArgumentException>(() => OrderingProjectionRebuildCheckpoint.Create(
            key,
            new ProjectionRebuildCheckpoint(
                new string('x', ProjectionRebuildCheckpointState.CursorMaxLength + 1),
                0,
                0,
                0,
                0,
                1,
                Now)));
    }

    private static CatalogItemProjectionWriteModel CreateWriteModel(
        string scopeId = "tenant-a",
        Guid? catalogItemId = null,
        string sku = "SKU-1",
        string name = "Projection item",
        decimal price = 10m,
        string currency = "USD",
        CatalogItemStatus status = CatalogItemStatus.Active) =>
        new(
            scopeId,
            catalogItemId ?? ItemId,
            sku,
            name,
            price,
            currency,
            status);
}
