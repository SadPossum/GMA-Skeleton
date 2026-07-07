namespace Ordering.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class OrderingUnitOfWork(OrderingDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<OrderingDbContext>(OrderingMigrations.Schema, dbContext, domainEventDispatcher)
{
}
