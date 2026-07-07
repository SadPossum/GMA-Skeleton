namespace Catalog.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class CatalogOutboxWriter(
    CatalogDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<CatalogDbContext>(dbContext, clock, applicationIdentity, CatalogMigrations.Schema, scopeResolvers);
