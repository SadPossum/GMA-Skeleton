namespace Gma.Modules.Administration.Application;

using Gma.Modules.Administration.Contracts;

public static class AdministrationAdminOperationNames
{
    public const string Bootstrap = AdministrationModuleMetadata.AdminSurfaceName + ".bootstrap";
    public const string RolesList = AdministrationModuleMetadata.AdminSurfaceName + ".roles.list";
    public const string RolesCreate = AdministrationModuleMetadata.AdminSurfaceName + ".roles.create";
    public const string RolesGrant = AdministrationModuleMetadata.AdminSurfaceName + ".roles.grant";
    public const string RolesAssign = AdministrationModuleMetadata.AdminSurfaceName + ".roles.assign";
}
