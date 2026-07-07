namespace Gma.Modules.Administration.Persistence;

using Gma.Modules.Administration.Contracts;

public static class AdminMigrations
{
    public const string Schema = AdministrationModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Gma.Modules.Administration.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Gma.Modules.Administration.Persistence.PostgreSqlMigrations";
}
