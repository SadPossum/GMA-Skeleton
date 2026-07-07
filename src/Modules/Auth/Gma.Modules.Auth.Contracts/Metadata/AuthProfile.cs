namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Naming;
using Gma.Framework.Tenancy;

public sealed record AuthProfile
{
    public const string GlobalProfileName = "global";
    public const string TenantScopedProfileName = "tenant-scoped";
    public const string DefaultGlobalScopeId = "global";

    private AuthProfile(
        string name,
        string? globalScopeId,
        bool requiresTenantContext,
        ModuleProfileDescriptor descriptor)
    {
        this.Name = name;
        this.GlobalScopeId = globalScopeId;
        this.RequiresTenantContext = requiresTenantContext;
        this.Descriptor = descriptor;
    }

    public string Name { get; }
    public string? GlobalScopeId { get; }
    public bool RequiresTenantContext { get; }
    public ModuleProfileDescriptor Descriptor { get; }

    public static AuthProfile Global(string scopeId = DefaultGlobalScopeId)
    {
        string normalizedScopeId = TenantIds.Normalize(scopeId);
        string provider = Provider(GlobalProfileName);
        return new AuthProfile(
            GlobalProfileName,
            normalizedScopeId,
            requiresTenantContext: false,
            new ModuleProfileDescriptor(
                AuthModuleMetadata.Name,
                GlobalProfileName,
                provides:
                [
                    AuthCompositionFeatures.MembersProvided(provider),
                    AuthCompositionFeatures.SessionsProvided(provider),
                    AuthCompositionFeatures.GlobalScopeProvided(provider)
                ],
                displayName: "Auth global",
                description: $"Stores all Auth members in the '{normalizedScopeId}' scope without requiring tenant context."));
    }

    public static AuthProfile TenantScoped()
    {
        string provider = Provider(TenantScopedProfileName);
        return new AuthProfile(
            TenantScopedProfileName,
            globalScopeId: null,
            requiresTenantContext: true,
            new ModuleProfileDescriptor(
                AuthModuleMetadata.Name,
                TenantScopedProfileName,
                provides:
                [
                    AuthCompositionFeatures.MembersProvided(provider),
                    AuthCompositionFeatures.SessionsProvided(provider),
                    AuthCompositionFeatures.TenantScopeProvided(provider)
                ],
                requires:
                [
                    new RequiredCompositionFeature(
                        TenancyCompositionFeatures.Context,
                        provider,
                        reason: "Register TenancyModule, or choose AuthProfile.Global(\"global\") for tenant-free projects.")
                ],
                displayName: "Auth tenant scoped",
                description: "Stores Auth members in the resolved tenant scope and requires tenant context."));
    }

    private static string Provider(string profileName) => $"{AuthModuleMetadata.Name}/{profileName}";
}
