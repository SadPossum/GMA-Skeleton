namespace Gma.Modules.Auth.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class AuthOutboxStore(AuthDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<AuthDbContext>(dbContext, options, AuthMigrations.Schema);
