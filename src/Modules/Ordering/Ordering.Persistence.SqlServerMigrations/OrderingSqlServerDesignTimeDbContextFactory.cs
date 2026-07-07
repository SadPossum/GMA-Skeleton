namespace Ordering.Persistence.SqlServerMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Ordering.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class OrderingSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        return new OrderingDbContext(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<OrderingDbContext>(
                args,
                OrderingMigrations.SqlServerAssembly,
                OrderingMigrations.Schema,
                OrderingMigrations.HistoryTable),
            new DesignTimeTenantContext());
    }
}
