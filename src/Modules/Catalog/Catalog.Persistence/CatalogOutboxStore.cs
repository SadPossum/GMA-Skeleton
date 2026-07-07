namespace Catalog.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class CatalogOutboxStore(CatalogDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<CatalogDbContext>(dbContext, options, CatalogMigrations.Schema);
