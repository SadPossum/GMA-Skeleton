namespace Gma.Modules.Notifications.Tests;

using Gma.Modules.Notifications.Domain.ValueObjects;
using Xunit;

[Trait("Category", "Unit")]
public sealed class NotificationSemanticEnumTests
{
    [Fact]
    public void Notification_severity_uses_unknown_default_and_stable_wire_names()
    {
        Assert.Equal(0, (int)NotificationSeverity.Unknown);
        Assert.True(NotificationSeverityNames.Parse(" Warning ").IsSuccess);
        Assert.Equal(NotificationSeverity.Warning, NotificationSeverityNames.Parse(" Warning ").Value);
        Assert.Equal("warning", NotificationSeverityNames.ToWireName(NotificationSeverity.Warning));
        Assert.True(NotificationSeverityNames.Parse("critical").IsFailure);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NotificationSeverityNames.ToWireName(NotificationSeverity.Unknown));
    }

    [Fact]
    public void Broadcast_audience_uses_unknown_default_and_stable_wire_names()
    {
        Assert.Equal(0, (int)NotificationBroadcastAudience.Unknown);
        Assert.True(NotificationBroadcastAudienceNames.Parse(" Tenant-Admins ").IsSuccess);
        Assert.Equal(
            NotificationBroadcastAudience.TenantAdmins,
            NotificationBroadcastAudienceNames.Parse(" Tenant-Admins ").Value);
        Assert.Equal(
            "tenant-admins",
            NotificationBroadcastAudienceNames.ToWireName(NotificationBroadcastAudience.TenantAdmins));
        Assert.True(NotificationBroadcastAudienceNames.IsTenantScoped(NotificationBroadcastAudience.TenantAdmins));
        Assert.True(NotificationBroadcastAudienceNames.TargetsAdmins(NotificationBroadcastAudience.PlatformAdmins));
        Assert.True(NotificationBroadcastAudienceNames.Parse("everyone").IsFailure);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NotificationBroadcastAudienceNames.ToWireName(NotificationBroadcastAudience.Unknown));
    }

    [Fact]
    public void Broadcast_recipient_kind_uses_unknown_default_and_stable_wire_names()
    {
        Assert.Equal(0, (int)NotificationBroadcastRecipientKind.Unknown);
        Assert.True(NotificationBroadcastRecipientKindNames.Parse(" Admin ").IsSuccess);
        Assert.Equal(
            NotificationBroadcastRecipientKind.Admin,
            NotificationBroadcastRecipientKindNames.Parse(" Admin ").Value);
        Assert.Equal(
            "admin",
            NotificationBroadcastRecipientKindNames.ToWireName(NotificationBroadcastRecipientKind.Admin));
        Assert.True(NotificationBroadcastRecipientKindNames.Parse("owner").IsFailure);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NotificationBroadcastRecipientKindNames.ToWireName(NotificationBroadcastRecipientKind.Unknown));
    }
}
