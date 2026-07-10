namespace Ordering.Contracts;

using Catalog.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Scoping;

public static class OrderingProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        OrderingModuleMetadata.Name,
        DefaultName,
        provides:
        [
            OrderingCompositionFeatures.OrdersProvided(Provider(DefaultName)),
            OrderingCompositionFeatures.CatalogItemProjectionsProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                ScopeCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Ordering is scope-aware; register a scope provider such as TenancyModule or Gma.Framework.Tenancy.Infrastructure."),
            CatalogCompositionFeatures.ItemsRequired(
                Provider(DefaultName),
                "Ordering decisions are based on Catalog-owned item facts copied into local projections."),
            MessagingCompositionFeatures.NatsConsumersRequired(
                Provider(DefaultName),
                "Live Catalog projection updates require the NATS consumer runtime; rebuild/manual projection loading can be used instead.",
                optional: true),
            TasksCompositionFeatures.WorkerRequired(
                Provider(DefaultName),
                "Catalog projection rebuild tasks require a task worker host; live consumers or manual backfill can be used instead.",
                optional: true),
            TasksCompositionFeatures.ScopeContextRequired(
                Provider(DefaultName),
                "Catalog projection rebuild tasks are scope-aware; compose a task scope provider such as Gma.Framework.Tenancy.Tasks in worker hosts that run them.",
                optional: true)
        ],
        requiredModules:
        [
            new RequiredCompositionModule(
                CatalogModuleMetadata.Name,
                Provider(DefaultName),
                reason: "Ordering example imports Catalog contracts and expects Catalog item facts as the source of projection truth.")
        ],
        displayName: "Ordering default",
        description: "Scope-aware ordering with local catalog item projections and optional live/rebuild projection maintenance.");

    private static string Provider(string profileName) => $"{OrderingModuleMetadata.Name}/{profileName}";
}
