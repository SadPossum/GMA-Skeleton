namespace Ordering.Persistence;

using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Aggregates;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Gma.Framework.Messaging.Infrastructure;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<OrderingDbContext>(options, scopeContext)
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
        this.ApplyScopeConventions(modelBuilder);
    }
}
