namespace Gma.Modules.Auth.Persistence;

using Gma.Modules.Auth.Contracts;

public static class AuthMigrations
{
    public const string Schema = AuthModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Gma.Modules.Auth.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Gma.Modules.Auth.Persistence.PostgreSqlMigrations";
}
