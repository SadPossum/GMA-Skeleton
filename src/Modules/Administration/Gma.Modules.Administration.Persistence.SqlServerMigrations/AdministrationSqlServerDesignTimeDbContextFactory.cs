namespace Gma.Modules.Administration.Persistence.SqlServerMigrations;

using Gma.Modules.Administration.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class AdministrationSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        return new AdminDbContext(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<AdminDbContext>(
                args,
                AdminMigrations.SqlServerAssembly,
                AdminMigrations.Schema,
                AdminMigrations.HistoryTable));
    }
}
