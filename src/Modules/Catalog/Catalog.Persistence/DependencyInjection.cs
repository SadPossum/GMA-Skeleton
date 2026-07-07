namespace Catalog.Persistence;

using Catalog.Application.Ports;
using Catalog.Contracts;
using Catalog.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Messaging;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Persistence.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<CatalogDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                CatalogMigrations.SqlServerAssembly,
                CatalogMigrations.PostgreSqlAssembly,
                CatalogMigrations.Schema,
                CatalogMigrations.HistoryTable));

        builder.Services.TryAddScoped<ICatalogItemRepository, CatalogItemRepository>();
        builder.Services.TryAddScoped<ICatalogItemReadRepository, CatalogItemReadRepository>();
        builder.Services.TryAddScoped<ICatalogItemProjectionExportSource, CatalogItemProjectionExportSource>();
        builder.Services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IUnitOfWork, CatalogUnitOfWork>(),
            ServiceDescriptor.Scoped<IOutboxWriter, CatalogOutboxWriter>(),
            ServiceDescriptor.Scoped<IOutboxStore, CatalogOutboxStore>(),
            ServiceDescriptor.Scoped<IInboxStore, CatalogInboxStore>()
        ]);

        return builder;
    }
}
