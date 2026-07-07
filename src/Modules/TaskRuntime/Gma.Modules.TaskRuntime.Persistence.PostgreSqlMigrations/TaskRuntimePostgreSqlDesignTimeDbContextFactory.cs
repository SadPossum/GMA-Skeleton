namespace Gma.Modules.TaskRuntime.Persistence.PostgreSqlMigrations;

using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.TaskRuntime.Persistence;

public sealed class TaskRuntimePostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TaskRuntimeDbContext>
{
    public TaskRuntimeDbContext CreateDbContext(string[] args)
    {
        return new TaskRuntimeDbContext(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<TaskRuntimeDbContext>(
                args,
                TaskRuntimeMigrations.PostgreSqlAssembly,
                TaskRuntimeMigrations.Schema,
                TaskRuntimeMigrations.HistoryTable));
    }
}
