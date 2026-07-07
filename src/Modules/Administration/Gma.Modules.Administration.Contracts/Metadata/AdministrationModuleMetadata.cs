namespace Gma.Modules.Administration.Contracts;

using Gma.Framework.Authorization;
using Gma.Framework.Modules;

public static class AdministrationModuleMetadata
{
    public const string Name = "administration";
    public const string Schema = "admin";
    public const string AdminSurfaceName = "admin";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithAdminSurfaceName(AdminSurfaceName)
        .WithPermissions([
            new ModulePermissionDescriptor(AdministrationPermissionCodes.Bootstrap, "Bootstrap the first administration owner.", tenantScoped: false),
            new ModulePermissionDescriptor(AdministrationPermissionCodes.RolesRead, "Read administration roles.", tenantScoped: false),
            new ModulePermissionDescriptor(AdministrationPermissionCodes.RolesManage, "Manage administration roles and assignments.", tenantScoped: false),
        ])
        .Build();
}
