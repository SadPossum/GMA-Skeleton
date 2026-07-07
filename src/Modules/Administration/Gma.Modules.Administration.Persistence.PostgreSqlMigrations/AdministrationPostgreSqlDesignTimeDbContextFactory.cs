namespace Gma.Modules.Administration.Persistence.PostgreSqlMigrations;

using Gma.Modules.Administration.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class AdministrationPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        return new AdminDbContext(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<AdminDbContext>(
                args,
                AdminMigrations.PostgreSqlAssembly,
                AdminMigrations.Schema,
                AdminMigrations.HistoryTable));
    }
}
