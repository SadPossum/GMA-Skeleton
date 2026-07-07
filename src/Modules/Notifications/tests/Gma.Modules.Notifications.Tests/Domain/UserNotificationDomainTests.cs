namespace Gma.Modules.Notifications.Tests;

using System.Text.Json;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UserNotificationDomainTests
{
    [Fact]
    public void Create_normalizes_notification_identity_and_payload()
    {
        Result<UserNotification> result = UserNotification.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            " tenant-a ",
            " user-a ",
            " Catalog ",
            "Catalog.Item-Updated",
            1,
            " Item updated ",
            "  The item changed.  ",
            NotificationSeverity.Success,
            new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 4, 12, 1, 0, TimeSpan.Zero),
            "{ \"sku\": \"SKU-1\" }");

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.TenantId);
        Assert.Equal("user-a", result.Value.Recipient.UserId);
        Assert.Equal("catalog", result.Value.Source.Module);
        Assert.Equal("catalog.item-updated", result.Value.Source.Name);
        Assert.Equal("Item updated", result.Value.Content.Title);
        Assert.Equal("The item changed.", result.Value.Content.Body);
        Assert.Equal(NotificationSeverity.Success, result.Value.Severity);
        using JsonDocument document = JsonDocument.Parse(result.Value.Payload.Json);
        Assert.Equal("SKU-1", document.RootElement.GetProperty("sku").GetString());
    }

    [Fact]
    public void Create_rejects_invalid_payload_json()
    {
        Result<UserNotification> result = UserNotification.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            "user-a",
            "catalog",
            "catalog.item-updated",
            1,
            "Item updated",
            null,
            NotificationSeverity.Info,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "{");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationsDomainErrors.PayloadInvalid, result.Error);
    }

    [Fact]
    public void Mark_read_is_idempotent()
    {
        UserNotification notification = CreateNotification();
        DateTimeOffset readAt = new(2026, 7, 4, 12, 2, 0, TimeSpan.Zero);

        Assert.True(notification.MarkRead(readAt));
        Assert.False(notification.MarkRead(readAt.AddMinutes(1)));
        Assert.Equal(readAt, notification.ReadAtUtc);
    }

    private static UserNotification CreateNotification() =>
        UserNotification.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            "user-a",
            "catalog",
            "catalog.item-updated",
            1,
            "Item updated",
            null,
            NotificationSeverity.Info,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "{}").Value;
}
