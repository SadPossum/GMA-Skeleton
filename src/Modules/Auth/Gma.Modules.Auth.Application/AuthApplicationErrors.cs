namespace Gma.Modules.Auth.Application;

using Gma.Modules.Auth.Domain.Errors;
using Gma.Framework.Results;

public static class AuthApplicationErrors
{
    public static readonly Error TenantRequired = new("Auth.TenantRequired", "A tenant id is required.");
    public static readonly Error UsernameTypeInvalid = new("Auth.UsernameTypeInvalid", "Username type must be email or phone.");
    public static readonly Error TokenInvalid = new("Auth.TokenInvalid", "Access token is invalid.");
    public static readonly Error TenantMismatch = new("Auth.TenantMismatch", "Access token tenant does not match the active tenant.");
    public static readonly Error CredentialsNotValid = AuthDomainErrors.CredentialsNotValid;
    public static readonly Error UsernameAlreadyExists = AuthDomainErrors.UsernameAlreadyExists;
    public static readonly Error MemberNotFound = AuthDomainErrors.MemberNotFound;
    public static readonly Error SessionNotFound = AuthDomainErrors.SessionNotFound;
    public static readonly Error SessionInactive = AuthDomainErrors.SessionInactive;
    public static readonly Error RefreshTokenInvalid = AuthDomainErrors.RefreshTokenInvalid;
    public static readonly Error RefreshTokenExpired = AuthDomainErrors.RefreshTokenExpired;
    public static readonly Error MemberStatusUnknown = AuthDomainErrors.MemberStatusUnknown;
    public static readonly Error MemberDisabled = AuthDomainErrors.MemberDisabled;
    public static readonly Error MemberAlreadyDisabled = AuthDomainErrors.MemberAlreadyDisabled;
    public static readonly Error MemberAlreadyActive = AuthDomainErrors.MemberAlreadyActive;
}
