namespace Gma.Modules.Auth.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class AuthUnitOfWork(AuthDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<AuthDbContext>(AuthMigrations.Schema, dbContext, domainEventDispatcher)
{
}
