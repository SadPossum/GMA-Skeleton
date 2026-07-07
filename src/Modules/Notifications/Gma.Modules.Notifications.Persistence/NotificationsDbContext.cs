namespace Gma.Modules.Notifications.Persistence;

using Microsoft.EntityFrameworkCore;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Entities;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Tenancy;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext<NotificationsDbContext>(options, tenantContext)
{
    public DbSet<UserNotification> UserNotifications => this.Set<UserNotification>();
    public DbSet<NotificationBroadcast> NotificationBroadcasts => this.Set<NotificationBroadcast>();
    public DbSet<NotificationBroadcastRead> NotificationBroadcastReads => this.Set<NotificationBroadcastRead>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(NotificationsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        this.ApplyTenantConventions(modelBuilder);
    }
}
