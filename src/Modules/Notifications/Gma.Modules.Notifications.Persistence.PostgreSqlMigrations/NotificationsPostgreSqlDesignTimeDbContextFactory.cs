namespace Gma.Modules.Notifications.Persistence.PostgreSqlMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Gma.Modules.Notifications.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class NotificationsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        return new NotificationsDbContext(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<NotificationsDbContext>(
                args,
                NotificationsMigrations.PostgreSqlAssembly,
                NotificationsMigrations.Schema,
                NotificationsMigrations.HistoryTable),
            new DesignTimeTenantContext());
    }
}
