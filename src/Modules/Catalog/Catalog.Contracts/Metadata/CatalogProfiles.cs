namespace Catalog.Contracts;

using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;

public static class CatalogProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        CatalogModuleMetadata.Name,
        DefaultName,
        provides:
        [
            CatalogCompositionFeatures.ItemsProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Catalog is tenant-scoped; register TenancyModule or at least Gma.Framework.Tenancy.Infrastructure."),
            CachingCompositionFeatures.TenantScopeRequired(
                Provider(DefaultName),
                "Catalog cache keys are tenant-owned; register Gma.Framework.Tenancy.Caching alongside Gma.Framework.Caching.Infrastructure or Gma.Framework.Caching.Cqrs."),
            CachingCompositionFeatures.ApplicationRequired(
                Provider(DefaultName),
                "Catalog read handlers use explicit cache-aside; register Gma.Framework.Caching.Infrastructure or Gma.Framework.Caching.Cqrs."),
            CachingCompositionFeatures.InvalidationRequired(
                Provider(DefaultName),
                "Catalog commands enqueue post-commit cache invalidations; register Gma.Framework.Caching.Infrastructure or Gma.Framework.Caching.Cqrs."),
            MessagingCompositionFeatures.OutboxRequired(
                Provider(DefaultName),
                "Catalog publishes integration events through its module outbox; register Gma.Framework.Messaging.Infrastructure.")
        ],
        displayName: "Catalog default",
        description: "Tenant-scoped catalog item management with explicit cache-aside and producer-owned outbox events.");

    private static string Provider(string profileName) => $"{CatalogModuleMetadata.Name}/{profileName}";
}
