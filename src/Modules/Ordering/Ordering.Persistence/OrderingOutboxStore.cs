namespace Ordering.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class OrderingOutboxStore(OrderingDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<OrderingDbContext>(dbContext, options, OrderingMigrations.Schema);
