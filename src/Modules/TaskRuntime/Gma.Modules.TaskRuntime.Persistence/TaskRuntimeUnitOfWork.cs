namespace Gma.Modules.TaskRuntime.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Modules.TaskRuntime.Contracts;

internal sealed class TaskRuntimeUnitOfWork(TaskRuntimeDbContext dbContext) : IUnitOfWork
{
    public string ModuleName => TaskRuntimeModuleMetadata.Name;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.ChangeTracker.HasChanges()
            ? dbContext.SaveChangesAsync(cancellationToken)
            : Task.CompletedTask;
}
