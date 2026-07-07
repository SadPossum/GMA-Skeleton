namespace Ordering.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;

internal sealed class OrderingOutboxWriter(
    OrderingDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<OrderingDbContext>(dbContext, clock, applicationIdentity, OrderingMigrations.Schema, scopeResolvers);
