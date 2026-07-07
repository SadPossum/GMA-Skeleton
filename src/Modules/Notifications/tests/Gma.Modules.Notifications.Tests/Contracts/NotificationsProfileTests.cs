namespace Gma.Modules.Notifications.Tests;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class NotificationsProfileTests
{
    [Fact]
    public void Default_profile_documents_durable_notification_module_capabilities()
    {
        ModuleProfileDescriptor profile = NotificationsProfiles.Default;

        Assert.Equal(NotificationsModuleMetadata.Name, profile.ModuleName);
        Assert.Equal(NotificationsProfiles.DefaultName, profile.ProfileName);
        Assert.Contains(profile.Provides, feature => feature.Id == NotificationsCompositionFeatures.History);
        Assert.Contains(profile.Provides, feature => feature.Id == NotificationsCompositionFeatures.Broadcasts);
        Assert.Contains(profile.Requires, feature => feature.Id == TenancyCompositionFeatures.Context);
    }

    [Fact]
    public void Descriptor_exposes_default_profile()
    {
        ModuleProfileDescriptor profile = Assert.Single(NotificationsModuleMetadata.Descriptor.GetCompositionProfiles());

        Assert.Equal(NotificationsProfiles.DefaultName, profile.ProfileName);
    }
}
