namespace Gma.Modules.Auth.Persistence.PostgreSqlMigrations;

using Gma.Modules.Auth.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class AuthPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        return new AuthDbContext(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<AuthDbContext>(
                args,
                AuthMigrations.PostgreSqlAssembly,
                AuthMigrations.Schema,
                AuthMigrations.HistoryTable),
            new DesignTimeTenantContext());
    }
}
