namespace Gma.Modules.Notifications.Persistence;

using Gma.Modules.Notifications.Contracts;

public static class NotificationsMigrations
{
    public const string Schema = NotificationsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Gma.Modules.Notifications.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Gma.Modules.Notifications.Persistence.PostgreSqlMigrations";
}
