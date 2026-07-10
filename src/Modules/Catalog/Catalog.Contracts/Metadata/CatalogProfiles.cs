namespace Catalog.Contracts;

using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Scoping;

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
                ScopeCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Catalog is scope-aware; register a scope provider such as TenancyModule or Gma.Framework.Tenancy.Infrastructure."),
            CachingCompositionFeatures.ScopeContextRequired(
                Provider(DefaultName),
                "Catalog cache keys are scope-aware; register a cache scope provider such as Gma.Framework.Tenancy.Caching alongside Gma.Framework.Caching.Infrastructure or Gma.Framework.Caching.Cqrs."),
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
        description: "Scope-aware catalog item management with explicit cache-aside and producer-owned outbox events.");

    private static string Provider(string profileName) => $"{CatalogModuleMetadata.Name}/{profileName}";
}
