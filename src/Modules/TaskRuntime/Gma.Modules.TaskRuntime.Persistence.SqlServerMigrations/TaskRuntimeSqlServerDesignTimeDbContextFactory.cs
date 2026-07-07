namespace Gma.Modules.TaskRuntime.Persistence.SqlServerMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.TaskRuntime.Persistence;

public sealed class TaskRuntimeSqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TaskRuntimeDbContext>
{
    public TaskRuntimeDbContext CreateDbContext(string[] args)
    {
        return new TaskRuntimeDbContext(
            DesignTimeDbContextOptionsFactory.CreateSqlServerOptions<TaskRuntimeDbContext>(
                args,
                TaskRuntimeMigrations.SqlServerAssembly,
                TaskRuntimeMigrations.Schema,
                TaskRuntimeMigrations.HistoryTable));
    }
}
