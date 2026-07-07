namespace Gma.Modules.Auth.Tests.Contracts;

using Gma.Modules.Auth.Api;
using Gma.Modules.Auth.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Modules.Tenancy.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthProfileTests
{
    [Fact]
    public void Global_profile_does_not_require_tenancy_context()
    {
        AuthProfile profile = AuthProfile.Global("global");

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(profile.Descriptor)]));

        Assert.True(result.IsValid);
        Assert.False(profile.RequiresTenantContext);
        Assert.Equal("global", profile.GlobalScopeId);
        Assert.Contains(profile.Descriptor.Provides, feature => feature.Id == AuthCompositionFeatures.GlobalScope);
    }

    [Fact]
    public void Global_profile_composes_without_tenancy_module_using_default_scope_context()
    {
        IHostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(CreateValidAuthConfiguration());

        builder.AddTenancyInfrastructure();
        builder.AddAuthModule(AuthProfile.Global("global"));

        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        SelectedModuleProfile selectedProfile = Assert.Single(scope.ServiceProvider.GetServices<SelectedModuleProfile>());
        TenantOptions tenantOptions = scope.ServiceProvider.GetRequiredService<IOptions<TenantOptions>>().Value;
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.Equal(AuthModuleMetadata.Name, selectedProfile.Profile.ModuleName);
        Assert.Equal(AuthProfile.GlobalProfileName, selectedProfile.Profile.ProfileName);
        Assert.False(tenantOptions.Enabled);
        Assert.Equal("global", tenantOptions.LocalDefaultTenantId);
        Assert.False(tenantContext.IsEnabled);
        Assert.Equal("global", tenantContext.TenantId);
    }

    [Fact]
    public void Tenant_scoped_profile_requires_tenancy_context()
    {
        AuthProfile profile = AuthProfile.TenantScoped();

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(profile.Descriptor)]));

        Assert.False(result.IsValid);
        Assert.Contains("tenancy.context", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Tenant_scoped_profile_is_satisfied_by_tenancy_profile()
    {
        AuthProfile profile = AuthProfile.TenantScoped();

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles:
            [
                new SelectedModuleProfile(TenancyProfiles.Default),
                new SelectedModuleProfile(profile.Descriptor)
            ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Tenancy_profile_advertises_header_resolution_separately_from_context()
    {
        Assert.Contains(TenancyProfiles.Default.Provides, feature => feature.Id == TenancyCompositionFeatures.Context);
        Assert.Contains(TenancyProfiles.Default.Provides, feature => feature.Id == TenancyCompositionFeatures.HeaderResolution);
    }

    private static IEnumerable<KeyValuePair<string, string?>> CreateValidAuthConfiguration() =>
    [
        new("Auth:Jwt:SigningKey", "test-jwt-signing-key-000000000000000000000000"),
        new("Auth:RefreshTokens:Pepper", "test-refresh-token-pepper-000000000000000000000000"),
        new("Persistence:Provider", "SqlServer"),
        new("ConnectionStrings:SqlServer", "Server=(localdb)\\mssqllocaldb;Database=GenericModularApiAuthProfileTests;Trusted_Connection=True;")
    ];
}
