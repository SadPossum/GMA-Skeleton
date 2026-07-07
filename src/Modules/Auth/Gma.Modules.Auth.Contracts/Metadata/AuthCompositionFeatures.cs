namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.ModuleComposition;

public static class AuthCompositionFeatures
{
    public static readonly CompositionFeatureId Members = new("auth.members");
    public static readonly CompositionFeatureId Sessions = new("auth.sessions");
    public static readonly CompositionFeatureId GlobalScope = new("auth.scope.global");
    public static readonly CompositionFeatureId TenantScope = new("auth.scope.tenant");

    public static ProvidedCompositionFeature MembersProvided(string provider) =>
        new(Members, provider, "Auth member account management.");

    public static ProvidedCompositionFeature SessionsProvided(string provider) =>
        new(Sessions, provider, "Auth member session and refresh-token management.");

    public static ProvidedCompositionFeature GlobalScopeProvided(string provider) =>
        new(GlobalScope, provider, "Auth stores all members in one configured global scope.");

    public static ProvidedCompositionFeature TenantScopeProvided(string provider) =>
        new(TenantScope, provider, "Auth stores members in the active tenant scope.");
}
