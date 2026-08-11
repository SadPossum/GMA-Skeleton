namespace Ordering.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class OrderingInboxStore(
    OrderingDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventInboxStore<OrderingDbContext>(
        dbContext,
        clock,
        idGenerator,
        domainEventDispatcher,
        OrderingMigrations.Schema);
