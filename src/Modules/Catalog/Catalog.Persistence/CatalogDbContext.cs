namespace Catalog.Persistence;

using Catalog.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Gma.Framework.Messaging.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<CatalogDbContext>(options, scopeContext)
{
    public DbSet<CatalogItem> CatalogItems => this.Set<CatalogItem>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(CatalogMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
