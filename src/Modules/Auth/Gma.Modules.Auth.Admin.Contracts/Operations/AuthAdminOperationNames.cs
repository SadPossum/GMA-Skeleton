namespace Gma.Modules.Auth.Admin.Contracts;

using Gma.Modules.Auth.Contracts;

public static class AuthAdminOperationNames
{
    public const string MembersList = AuthModuleMetadata.Name + ".members.list";
    public const string MembersGet = AuthModuleMetadata.Name + ".members.get";
    public const string MembersCreate = AuthModuleMetadata.Name + ".members.create";
    public const string MembersDisable = AuthModuleMetadata.Name + ".members.disable";
    public const string MembersEnable = AuthModuleMetadata.Name + ".members.enable";
    public const string MembersResetPassword = AuthModuleMetadata.Name + ".members.reset-password";
    public const string MembersRevokeSessions = AuthModuleMetadata.Name + ".members.revoke-sessions";
}
