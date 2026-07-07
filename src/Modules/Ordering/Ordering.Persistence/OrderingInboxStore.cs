namespace Ordering.Persistence;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class OrderingInboxStore(OrderingDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<OrderingDbContext>(dbContext, clock, idGenerator, OrderingMigrations.Schema);
