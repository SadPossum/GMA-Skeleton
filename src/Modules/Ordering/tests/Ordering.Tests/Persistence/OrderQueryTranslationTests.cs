namespace Ordering.Tests;

using Microsoft.EntityFrameworkCore;
using Ordering.Domain.ValueObjects;
using Ordering.Domain.Visibility;
using Ordering.Persistence;
using Ordering.Persistence.QueryScopes;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderQueryTranslationTests
{
    [Fact]
    public void User_orders_scope_translates_to_tenant_and_user_filters()
    {
        DbContextOptions<OrderingDbContext> options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseSqlServer("Server=localhost;Database=query-translation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using OrderingDbContext dbContext = new(options, new TestTenantContext("tenant-a"));
        UserOrdersScope scope = new("tenant-a", OrderUserId.Create("user-a").Value);

        string sql = dbContext.Orders
            .ApplyUserOrdersScope(scope)
            .AsNoTracking()
            .ToQueryString();

        Assert.Contains("ScopeId", sql, StringComparison.Ordinal);
        Assert.Contains("UserId", sql, StringComparison.Ordinal);
    }

    private sealed class TestTenantContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId { get; } = scopeId;
    }
}
