namespace Gma.Modules.Auth.Persistence.SqlServerMigrations;

using Gma.Modules.Auth.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class AuthSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        return new AuthDbContext(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<AuthDbContext>(
                args,
                AuthMigrations.SqlServerAssembly,
                AuthMigrations.Schema,
                AuthMigrations.HistoryTable),
            new DesignTimeTenantContext());
    }
}
