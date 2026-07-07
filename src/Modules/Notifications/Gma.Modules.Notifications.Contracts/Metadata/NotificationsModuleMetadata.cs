namespace Gma.Modules.Notifications.Contracts;

using Gma.Framework.Authorization;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;

public static class NotificationsModuleMetadata
{
    public const string Name = "notifications";
    public const string Schema = "notifications";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(
                NotificationsAdminPermissionCodes.HistoryRead,
                "Read tenant notification history.",
                tenantScoped: true),
            new ModulePermissionDescriptor(
                NotificationsAdminPermissionCodes.BroadcastsRead,
                "Read notification broadcasts.",
                tenantScoped: true),
            new ModulePermissionDescriptor(
                NotificationsAdminPermissionCodes.BroadcastsCreate,
                "Create notification broadcasts.",
                tenantScoped: true),
        ])
        .WithProfile(NotificationsProfiles.Default)
        .Build();
}
