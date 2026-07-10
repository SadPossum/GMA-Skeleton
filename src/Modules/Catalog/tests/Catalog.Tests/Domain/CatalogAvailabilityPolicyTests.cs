namespace Catalog.Tests;

using Catalog.Domain.Errors;
using Catalog.Domain.ValueObjects;
using Catalog.Domain.Visibility;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogAvailabilityPolicyTests
{
    [Theory]
    [InlineData("US", "us")]
    [InlineData(" eu ", "EU")]
    public void Can_view_available_items_returns_scope_for_matching_region(
        string requestedRegion,
        string viewerRegion)
    {
        CatalogViewer viewer = CatalogViewer.User(" user-1 ", " tenant-a ", viewerRegion).Value;

        Result<AvailableCatalogItemsScope> result =
            CatalogAvailabilityPolicy.CanViewAvailableItems(viewer, requestedRegion);

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.ScopeId);
        Assert.Equal(CatalogUserId.Create("user-1").Value, viewer.UserId);
        Assert.Equal(CatalogRegionCode.Create(viewerRegion).Value, result.Value.Region);
    }

    [Theory]
    [InlineData("US", "EU")]
    [InlineData("US", null)]
    public void Can_view_available_items_denies_mismatched_or_missing_viewer_region(
        string requestedRegion,
        string? viewerRegion)
    {
        Result<CatalogViewer> viewer = CatalogViewer.User("user-1", "tenant-a", viewerRegion);

        Result<AvailableCatalogItemsScope> result = viewer.IsFailure
            ? Result.Failure<AvailableCatalogItemsScope>(viewer.Error)
            : CatalogAvailabilityPolicy.CanViewAvailableItems(viewer.Value, requestedRegion);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogDomainErrors.AccessDenied, result.Error);
    }

    [Fact]
    public void Viewer_rejects_invalid_tenant_before_scope_creation()
    {
        Result<CatalogViewer> viewer = CatalogViewer.User("user-1", "tenant a", "US");

        Assert.True(viewer.IsFailure);
        Assert.Equal(CatalogDomainErrors.TenantInvalid, viewer.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("user 1")]
    [InlineData("user\t1")]
    public void Viewer_maps_invalid_user_id_to_access_denied(string userId)
    {
        Result<CatalogViewer> viewer = CatalogViewer.User(userId, "tenant-a", "US");

        Assert.True(viewer.IsFailure);
        Assert.Equal(CatalogDomainErrors.AccessDenied, viewer.Error);
    }

    [Fact]
    public void Can_view_available_items_rejects_invalid_requested_region()
    {
        CatalogViewer viewer = CatalogViewer.User("user-1", "tenant-a", "US").Value;

        Result<AvailableCatalogItemsScope> result =
            CatalogAvailabilityPolicy.CanViewAvailableItems(viewer, "-us");

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogDomainErrors.RegionInvalid, result.Error);
    }
}
