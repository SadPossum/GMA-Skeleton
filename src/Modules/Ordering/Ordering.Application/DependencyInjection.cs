namespace Ordering.Application;

using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Handlers;
using Ordering.Application.Tasks;
using Ordering.Contracts;
using Gma.Framework.Application.Composition;
using Gma.Framework.Messaging;
using Gma.Framework.ProjectionRebuild.Tasks;
using Gma.Framework.Tasks;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<CatalogItemChangeNotificationPublisher>();
        services.AddIntegrationEventHandler<CatalogItemCreatedIntegrationEvent, CatalogItemCreatedProjectionHandler>(
            OrderingModuleMetadata.Name,
            CatalogModuleMetadata.Name);
        services.AddIntegrationEventHandler<CatalogItemUpdatedIntegrationEvent, CatalogItemUpdatedProjectionHandler>(
            OrderingModuleMetadata.Name,
            CatalogModuleMetadata.Name);
        services.AddIntegrationEventHandler<CatalogItemDiscontinuedIntegrationEvent, CatalogItemDiscontinuedProjectionHandler>(
            OrderingModuleMetadata.Name,
            CatalogModuleMetadata.Name);

        return services;
    }

    public static IServiceCollection AddOrderingTaskHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProjectionRebuildTasks();
        services.AddTaskHandler<RebuildCatalogItemProjectionPayload, RebuildCatalogItemProjectionTaskHandler>(
            OrderingModuleMetadata.Name);

        return services;
    }
}
