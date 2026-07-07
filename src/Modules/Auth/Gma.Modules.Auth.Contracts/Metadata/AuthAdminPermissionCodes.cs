namespace Gma.Modules.Auth.Contracts;

public static class AuthAdminPermissionCodes
{
    public const string MembersRead = AuthModuleMetadata.Name + ".members.read";
    public const string MembersCreate = AuthModuleMetadata.Name + ".members.create";
    public const string MembersDisable = AuthModuleMetadata.Name + ".members.disable";
    public const string MembersEnable = AuthModuleMetadata.Name + ".members.enable";
    public const string MembersResetPassword = AuthModuleMetadata.Name + ".members.reset-password";
    public const string MembersRevokeSessions = AuthModuleMetadata.Name + ".members.revoke-sessions";
}
