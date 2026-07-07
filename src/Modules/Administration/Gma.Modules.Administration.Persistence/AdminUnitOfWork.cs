namespace Gma.Modules.Administration.Persistence;

using Gma.Modules.Administration.Contracts;
using Gma.Framework.Cqrs.UnitOfWork;

internal sealed class AdminUnitOfWork(AdminDbContext dbContext) : IUnitOfWork
{
    public string ModuleName => AdministrationModuleMetadata.Name;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
