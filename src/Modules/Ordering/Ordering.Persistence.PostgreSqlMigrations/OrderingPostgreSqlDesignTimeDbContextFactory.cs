namespace Ordering.Persistence.PostgreSqlMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Ordering.Persistence;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class OrderingPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        return new OrderingDbContext(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<OrderingDbContext>(
                args,
                OrderingMigrations.PostgreSqlAssembly,
                OrderingMigrations.Schema,
                OrderingMigrations.HistoryTable),
            new DesignTimeScopeContext());
    }
}
