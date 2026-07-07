namespace Gma.Modules.Auth.Domain.Errors;

using Gma.Framework.Results;

public static class AuthDomainErrors
{
    public static readonly Error MemberIdRequired = new("Auth.MemberIdRequired", "A member id is required.");
    public static readonly Error UsernameIdRequired = new("Auth.UsernameIdRequired", "A username id is required.");
    public static readonly Error SessionIdRequired = new("Auth.SessionIdRequired", "A session id is required.");
    public static readonly Error DomainEventIdRequired = new("Gma.Modules.Auth.DomainEventIdRequired", "A domain event id is required.");
    public static readonly Error TenantRequired = new("Auth.TenantRequired", "Tenant id is required.");
    public static readonly Error TenantInvalid = new("Auth.TenantInvalid", "Tenant id is not valid.");
    public static readonly Error PasswordNotValid = new("Auth.PasswordNotValid", "Password is not valid.");
    public static readonly Error CredentialsNotValid = new("Auth.CredentialsNotValid", "Username or password is incorrect.");
    public static readonly Error UsernameNotValid = new("Auth.UsernameNotValid", "Username is not valid.");
    public static readonly Error UsernameAlreadyExists = new("Auth.UsernameAlreadyExists", "Username is already registered.");
    public static readonly Error MemberNotFound = new("Auth.MemberNotFound", "Member was not found.");
    public static readonly Error SessionNotFound = new("Auth.SessionNotFound", "Session was not found.");
    public static readonly Error SessionInactive = new("Auth.SessionInactive", "Session is not active.");
    public static readonly Error RefreshTokenInvalid = new("Auth.RefreshTokenInvalid", "Refresh token is invalid.");
    public static readonly Error RefreshTokenHashNotValid = new("Auth.RefreshTokenHashNotValid", "Refresh token hash is not valid.");
    public static readonly Error RefreshTokenExpired = new("Auth.RefreshTokenExpired", "Refresh token has expired.");
    public static readonly Error MemberStatusUnknown = new("Auth.MemberStatusUnknown", "Member status is unknown.");
    public static readonly Error MemberDisabled = new("Auth.MemberDisabled", "Member is disabled.");
    public static readonly Error MemberAlreadyDisabled = new("Auth.MemberAlreadyDisabled", "Member is already disabled.");
    public static readonly Error MemberAlreadyActive = new("Auth.MemberAlreadyActive", "Member is already active.");
    public static readonly Error DisableReasonRequired = new("Auth.DisableReasonRequired", "A disable reason is required.");
    public static readonly Error DisableReasonTooLong = new("Auth.DisableReasonTooLong", "Disable reason must be 512 characters or fewer.");
}
