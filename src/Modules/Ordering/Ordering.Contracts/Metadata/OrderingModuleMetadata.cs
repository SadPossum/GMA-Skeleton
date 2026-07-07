namespace Ordering.Contracts;

using Catalog.Contracts;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Tasks;

public static class OrderingModuleMetadata
{
    public const string Name = "ordering";
    public const string Schema = "ordering";
    public const string CatalogItemProjectionName = "catalog-item-projections";
    public const int CatalogItemProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";
    public const string CatalogItemCreatedProjectionHandlerName = "catalog-item-created-projection";
    public const string CatalogItemUpdatedProjectionHandlerName = "catalog-item-updated-projection";
    public const string CatalogItemDiscontinuedProjectionHandlerName = "catalog-item-discontinued-projection";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithSubscription<CatalogItemCreatedIntegrationEvent>(CatalogModuleMetadata.Name, CatalogItemCreatedProjectionHandlerName)
        .WithSubscription<CatalogItemUpdatedIntegrationEvent>(CatalogModuleMetadata.Name, CatalogItemUpdatedProjectionHandlerName)
        .WithSubscription<CatalogItemDiscontinuedIntegrationEvent>(CatalogModuleMetadata.Name, CatalogItemDiscontinuedProjectionHandlerName)
        .WithPublishedEvent<UserNotificationRequestedIntegrationEvent>()
        .WithTask<RebuildCatalogItemProjectionPayload>()
        .WithProfile(OrderingProfiles.Default)
        .Build();
}
