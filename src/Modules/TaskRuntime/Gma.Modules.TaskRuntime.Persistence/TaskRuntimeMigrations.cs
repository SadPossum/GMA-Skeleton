namespace Gma.Modules.TaskRuntime.Persistence;

using Gma.Modules.TaskRuntime.Contracts;

public static class TaskRuntimeMigrations
{
    public const string Schema = TaskRuntimeModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Gma.Modules.TaskRuntime.Persistence.PostgreSqlMigrations";
}
