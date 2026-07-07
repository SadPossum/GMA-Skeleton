namespace Gma.Modules.Administration.Application;

using Gma.Modules.Administration.Contracts;
using Gma.Framework.Administration;

public static class AdministrationPermissions
{
    public static readonly AdminPermission Bootstrap = AdminPermission.Create(AdministrationPermissionCodes.Bootstrap);
    public static readonly AdminPermission RolesRead = AdminPermission.Create(AdministrationPermissionCodes.RolesRead);
    public static readonly AdminPermission RolesManage = AdminPermission.Create(AdministrationPermissionCodes.RolesManage);
}
