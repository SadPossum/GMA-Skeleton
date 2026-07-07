namespace Gma.Modules.Notifications.Tests;

using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Results;
using Xunit;
using ContractAudience = Gma.Modules.Notifications.Contracts.NotificationBroadcastAudience;

[Trait("Category", "Unit")]
public sealed class NotificationBroadcastDomainTests
{
    [Fact]
    public void Create_tenant_broadcast_normalizes_scope_and_payload()
    {
        Result<NotificationBroadcast> result = NotificationBroadcast.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            " tenant-a ",
            NotificationBroadcastAudience.TenantUsers,
            " Notifications ",
            "System.Maintenance",
            1,
            " Maintenance ",
            "  Scheduled window.  ",
            NotificationSeverity.Warning,
            new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 4, 12, 1, 0, TimeSpan.Zero),
            "{ \"window\": \"night\" }");

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.TenantId);
        Assert.Equal(NotificationBroadcastAudience.TenantUsers, result.Value.Audience);
        Assert.Equal("notifications", result.Value.Source.Module);
        Assert.Equal("system.maintenance", result.Value.Source.Name);
        Assert.Equal("Maintenance", result.Value.Content.Title);
        Assert.Equal("Scheduled window.", result.Value.Content.Body);
        Assert.Equal(NotificationSeverity.Warning, result.Value.Severity);
    }

    [Fact]
    public void Create_platform_broadcast_rejects_tenant_id()
    {
        Result<NotificationBroadcast> result = NotificationBroadcast.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            NotificationBroadcastAudience.PlatformUsers,
            "notifications",
            "system.maintenance",
            1,
            "Maintenance",
            null,
            NotificationSeverity.Info,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "{}");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.PlatformBroadcastTenantForbidden, result.Error);
    }

    [Fact]
    public void Public_broadcast_audience_has_unknown_default()
    {
        Assert.Equal(0, (int)ContractAudience.Unknown);
    }

    [Fact]
    public void Create_broadcast_rejects_payload_over_limit()
    {
        string payloadJson = "{\"data\":\"" + new string('a', NotificationPayload.MaxLength) + "\"}";

        Result<NotificationBroadcast> result = NotificationBroadcast.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            NotificationBroadcastAudience.TenantUsers,
            "notifications",
            "system.maintenance",
            1,
            "Maintenance",
            null,
            NotificationSeverity.Info,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            payloadJson);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.PayloadInvalid, result.Error);
    }
}
