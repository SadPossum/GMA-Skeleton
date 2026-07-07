namespace Gma.Modules.Auth.Persistence;

using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Tenancy;
using Gma.Framework.Messaging.Infrastructure;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext<AuthDbContext>(options, tenantContext)
{
    public DbSet<Member> Members => this.Set<Member>();
    public DbSet<MemberUsername> MemberUsernames => this.Set<MemberUsername>();
    public DbSet<MemberSession> MemberSessions => this.Set<MemberSession>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuthMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
        this.ApplyTenantConventions(modelBuilder);
    }
}
