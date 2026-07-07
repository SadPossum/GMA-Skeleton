namespace Catalog.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class CatalogUnitOfWork(CatalogDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<CatalogDbContext>(CatalogMigrations.Schema, dbContext, domainEventDispatcher)
{
}
