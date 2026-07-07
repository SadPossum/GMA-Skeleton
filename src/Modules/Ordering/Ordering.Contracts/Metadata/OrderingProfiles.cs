namespace Ordering.Contracts;

using Catalog.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;

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
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Ordering is tenant-scoped; register TenancyModule or at least Gma.Framework.Tenancy.Infrastructure."),
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
            TasksCompositionFeatures.TenantScopeRequired(
                Provider(DefaultName),
                "Catalog projection rebuild tasks are tenant-scoped; compose Gma.Framework.Tenancy.Tasks in worker hosts that run them.",
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
        description: "Tenant-scoped ordering with local catalog item projections and optional live/rebuild projection maintenance.");

    private static string Provider(string profileName) => $"{OrderingModuleMetadata.Name}/{profileName}";
}
