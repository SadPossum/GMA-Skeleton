namespace Gma.Modules.Notifications.Persistence;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs.UnitOfWork;

internal sealed class NotificationsUnitOfWork(NotificationsDbContext dbContext) : IUnitOfWork
{
    public string ModuleName => NotificationsModuleMetadata.Name;

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
