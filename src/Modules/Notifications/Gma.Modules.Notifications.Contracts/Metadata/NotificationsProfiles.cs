namespace Gma.Modules.Notifications.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Tenancy;

public static class NotificationsProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        NotificationsModuleMetadata.Name,
        DefaultName,
        provides:
        [
            NotificationsCompositionFeatures.HistoryProvided(Provider(DefaultName)),
            NotificationsCompositionFeatures.BroadcastsProvided(Provider(DefaultName))
        ],
        requires:
        [
            new RequiredCompositionFeature(
                TenancyCompositionFeatures.Context,
                Provider(DefaultName),
                reason: "Notifications history and broadcast inboxes are tenant-aware; register TenancyModule or at least Gma.Framework.Tenancy.Infrastructure.")
        ],
        displayName: "Notifications default",
        description: "Tenant-aware durable notification history, broadcasts, read state, and admin/public stream cursors.");

    private static string Provider(string profileName) => $"{NotificationsModuleMetadata.Name}/{profileName}";
}
