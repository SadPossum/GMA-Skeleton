namespace Gma.Modules.Auth.Admin.Contracts;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Administration;

public static class AuthAdminPermissions
{
    public static readonly AdminPermission MembersRead = AdminPermission.Create(AuthAdminPermissionCodes.MembersRead);
    public static readonly AdminPermission MembersCreate = AdminPermission.Create(AuthAdminPermissionCodes.MembersCreate);
    public static readonly AdminPermission MembersDisable = AdminPermission.Create(AuthAdminPermissionCodes.MembersDisable);
    public static readonly AdminPermission MembersEnable = AdminPermission.Create(AuthAdminPermissionCodes.MembersEnable);
    public static readonly AdminPermission MembersResetPassword = AdminPermission.Create(AuthAdminPermissionCodes.MembersResetPassword);
    public static readonly AdminPermission MembersRevokeSessions = AdminPermission.Create(AuthAdminPermissionCodes.MembersRevokeSessions);
}
