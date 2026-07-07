namespace Gma.Modules.Auth.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class AuthOutboxWriter(
    AuthDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<AuthDbContext>(dbContext, clock, applicationIdentity, AuthMigrations.Schema, scopeResolvers);
