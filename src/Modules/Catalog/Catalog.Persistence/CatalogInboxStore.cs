namespace Catalog.Persistence;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class CatalogInboxStore(CatalogDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<CatalogDbContext>(dbContext, clock, idGenerator, CatalogMigrations.Schema);
