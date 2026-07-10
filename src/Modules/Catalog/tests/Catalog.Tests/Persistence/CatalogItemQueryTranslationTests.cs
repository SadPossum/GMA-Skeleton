namespace Catalog.Tests;

using Catalog.Domain.ValueObjects;
using Catalog.Domain.Visibility;
using Catalog.Persistence;
using Catalog.Persistence.QueryScopes;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogItemQueryTranslationTests
{
    [Fact]
    public void Projection_export_cursor_query_translates_with_sku_value_object()
    {
        DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost;Database=query-translation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using CatalogDbContext dbContext = new(options, new TestTenantContext("tenant-a"));
        CatalogSku cursorSku = CatalogSku.Create("SKU-1").Value;

        string sql = dbContext.CatalogItems
            .FromSqlInterpolated($"""
                SELECT *
                FROM [catalog].[items]
                WHERE [Sku] > {cursorSku.Value}
                """)
            .AsNoTracking()
            .OrderBy(item => item.Sku)
            .ToQueryString();

        Assert.Contains("Sku", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Available_items_scope_translates_to_tenant_status_and_region_filters()
    {
        DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost;Database=query-translation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using CatalogDbContext dbContext = new(options, new TestTenantContext("tenant-a"));
        AvailableCatalogItemsScope scope = new("tenant-a", CatalogRegionCode.Create("US").Value);

        string sql = dbContext.CatalogItems
            .ApplyAvailableCatalogItemsScope(scope)
            .AsNoTracking()
            .ToQueryString();

        Assert.Contains("ScopeId", sql, StringComparison.Ordinal);
        Assert.Contains("Status", sql, StringComparison.Ordinal);
        Assert.Contains("RegionCode", sql, StringComparison.Ordinal);
    }

    private sealed class TestTenantContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId { get; } = scopeId;
    }
}
