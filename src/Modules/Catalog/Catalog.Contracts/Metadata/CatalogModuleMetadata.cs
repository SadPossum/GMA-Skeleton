namespace Catalog.Contracts;

using Gma.Framework.Permissions;
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
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsRead, "Read catalog items.", scopeRequirement: PermissionScopeRequirement.Scoped),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsCreate, "Create catalog items.", scopeRequirement: PermissionScopeRequirement.Scoped),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsUpdate, "Update catalog items.", scopeRequirement: PermissionScopeRequirement.Scoped),
            new ModulePermissionDescriptor(CatalogAdminPermissionCodes.ItemsDiscontinue, "Discontinue catalog items.", scopeRequirement: PermissionScopeRequirement.Scoped),
        ])
        .WithPublishedEvent<CatalogItemCreatedIntegrationEvent>()
        .WithPublishedEvent<CatalogItemUpdatedIntegrationEvent>()
        .WithPublishedEvent<CatalogItemDiscontinuedIntegrationEvent>()
        .WithCacheEntries([
            new ModuleCacheDescriptor(ItemsCacheEntry, CacheScope.Scope, [ItemsCacheTag]),
            new ModuleCacheDescriptor(ItemCacheEntry, CacheScope.Scope, [ItemsCacheTag]),
        ])
        .WithProfile(CatalogProfiles.Default)
        .Build();
}
