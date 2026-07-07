namespace Ordering.Tests;

using Catalog.Contracts;
using Ordering.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderingProfileTests
{
    [Fact]
    public void Default_profile_is_strict_about_owned_projection_sources_but_optional_about_maintenance_runtime()
    {
        ModuleProfileDescriptor profile = OrderingProfiles.Default;

        Assert.Equal(OrderingModuleMetadata.Name, profile.ModuleName);
        Assert.Equal(OrderingProfiles.DefaultName, profile.ProfileName);
        Assert.Contains(profile.Provides, feature => feature.Id == OrderingCompositionFeatures.Orders);
        Assert.Contains(profile.Provides, feature => feature.Id == OrderingCompositionFeatures.CatalogItemProjections);
        Assert.Contains(profile.Requires, feature => feature.Id == TenancyCompositionFeatures.Context && !feature.Optional);
        Assert.Contains(profile.Requires, feature => feature.Id == CatalogCompositionFeatures.Items && !feature.Optional);
        Assert.Contains(profile.Requires, feature => feature.Id == MessagingCompositionFeatures.NatsConsumers && feature.Optional);
        Assert.Contains(profile.Requires, feature => feature.Id == TasksCompositionFeatures.Worker && feature.Optional);
        Assert.Contains(profile.RequiredModules, module => module.ModuleName == CatalogModuleMetadata.Name);
    }

    [Fact]
    public void Descriptor_exposes_default_profile()
    {
        ModuleProfileDescriptor profile = Assert.Single(OrderingModuleMetadata.Descriptor.GetCompositionProfiles());

        Assert.Equal(OrderingProfiles.DefaultName, profile.ProfileName);
    }
}
