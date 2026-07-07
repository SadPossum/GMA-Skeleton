namespace Gma.Modules.TaskRuntime.Persistence;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Tasks.Infrastructure;

public sealed class TaskRuntimeDbContext(DbContextOptions<TaskRuntimeDbContext> options) : DbContext(options)
{
    public DbSet<TaskRun> TaskRuns => this.Set<TaskRun>();
    public DbSet<TaskControlMessageState> TaskControlMessages => this.Set<TaskControlMessageState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TaskRuntimeMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskRuntimeDbContext).Assembly);
    }
}
