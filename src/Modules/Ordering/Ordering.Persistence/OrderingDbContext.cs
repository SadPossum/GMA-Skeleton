namespace Ordering.Persistence;

using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Aggregates;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Tenancy;
using Gma.Framework.Messaging.Infrastructure;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext<OrderingDbContext>(options, tenantContext)
{
    public DbSet<Order> Orders => this.Set<Order>();
    public DbSet<CatalogItemProjection> CatalogItemProjections => this.Set<CatalogItemProjection>();
    public DbSet<OrderingProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<OrderingProjectionRebuildCheckpoint>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(OrderingMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
        this.ApplyTenantConventions(modelBuilder);
    }
}
