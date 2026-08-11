namespace Catalog.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class CatalogInboxStore(
    CatalogDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventInboxStore<CatalogDbContext>(
        dbContext,
        clock,
        idGenerator,
        domainEventDispatcher,
        CatalogMigrations.Schema);
