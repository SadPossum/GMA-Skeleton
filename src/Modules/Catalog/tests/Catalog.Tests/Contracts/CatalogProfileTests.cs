namespace Catalog.Tests;

using Catalog.Contracts;
using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CatalogProfileTests
{
    [Fact]
    public void Default_profile_documents_catalog_runtime_dependencies()
    {
        ModuleProfileDescriptor profile = CatalogProfiles.Default;

        Assert.Equal(CatalogModuleMetadata.Name, profile.ModuleName);
        Assert.Equal(CatalogProfiles.DefaultName, profile.ProfileName);
        Assert.Contains(profile.Provides, feature => feature.Id == CatalogCompositionFeatures.Items);
        Assert.Contains(profile.Requires, feature => feature.Id == TenancyCompositionFeatures.Context);
        Assert.Contains(profile.Requires, feature => feature.Id == CachingCompositionFeatures.Application);
        Assert.Contains(profile.Requires, feature => feature.Id == CachingCompositionFeatures.Invalidation);
        Assert.Contains(profile.Requires, feature => feature.Id == MessagingCompositionFeatures.Outbox);
    }

    [Fact]
    public void Descriptor_exposes_default_profile()
    {
        ModuleProfileDescriptor profile = Assert.Single(CatalogModuleMetadata.Descriptor.GetCompositionProfiles());

        Assert.Equal(CatalogProfiles.DefaultName, profile.ProfileName);
    }
}
