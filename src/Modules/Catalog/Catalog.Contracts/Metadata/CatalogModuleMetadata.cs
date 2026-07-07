namespace Catalog.Contracts;

using Gma.Framework.Authorization;
using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;

public static class CatalogModuleMetadata
{
    public const string Name = "catalog";
    public const string Schema = "catalog";
    public const string ItemsCacheTag = "catalog.items";
    public const string ItemsCacheEntry = "items";
    public const string ItemCacheEntry = "item";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsRead, "Read catalog items.", tenantScoped: true),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsCreate, "Create catalog items.", tenantScoped: true),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsUpdate, "Update catalog items.", tenantScoped: true),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsDiscontinue, "Discontinue catalog items.", tenantScoped: true),
        ])
        .WithPublishedEvent<CatalogItemCreatedIntegrationEvent>()
        .WithPublishedEvent<CatalogItemUpdatedIntegrationEvent>()
        .WithPublishedEvent<CatalogItemDiscontinuedIntegrationEvent>()
        .WithCacheEntries([
            new ModuleCacheDescriptor(ItemsCacheEntry, CacheScope.Tenant, [ItemsCacheTag]),
            new ModuleCacheDescriptor(ItemCacheEntry, CacheScope.Tenant, [ItemsCacheTag]),
        ])
        .WithProfile(CatalogProfiles.Default)
        .Build();
}
