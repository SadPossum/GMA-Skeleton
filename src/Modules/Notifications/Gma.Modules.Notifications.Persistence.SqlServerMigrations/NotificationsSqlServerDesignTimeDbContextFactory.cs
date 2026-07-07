namespace Gma.Modules.Notifications.Persistence.SqlServerMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Gma.Modules.Notifications.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class NotificationsSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        return new NotificationsDbContext(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<NotificationsDbContext>(
                args,
                NotificationsMigrations.SqlServerAssembly,
                NotificationsMigrations.Schema,
                NotificationsMigrations.HistoryTable),
            new DesignTimeTenantContext());
    }
}
