namespace Gma.Modules.Notifications.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class UserNotificationRequestedIntegrationEventTests
{
    [Fact]
    public void Event_normalizes_identity_metadata_and_json_payload()
    {
        UserNotificationRequestedIntegrationEvent integrationEvent = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            " tenant-a ",
            new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
            " user-a ",
            " Catalog ",
            " Catalog-Item-Updated ",
            1,
            " Item updated ",
            "  Catalog item changed.  ",
            NotificationSeverity.Success,
            "{ \"sku\": \"SKU-1\" }");

        Assert.Equal("tenant-a", integrationEvent.TenantId);
        Assert.Equal("user-a", integrationEvent.UserId);
        Assert.Equal("catalog", integrationEvent.SourceModule);
        Assert.Equal("catalog-item-updated", integrationEvent.NotificationName);
        Assert.Equal("Item updated", integrationEvent.Title);
        Assert.Equal("Catalog item changed.", integrationEvent.Body);
        Assert.Equal(NotificationSeverity.Success, integrationEvent.Severity);
        Assert.Equal("{\"sku\":\"SKU-1\"}", integrationEvent.PayloadJson);
    }

    [Fact]
    public void Event_uses_notification_name_rules_for_durable_request_name()
    {
        UserNotificationRequestedIntegrationEvent integrationEvent = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            DateTimeOffset.UtcNow,
            "user-a",
            "billing",
            " Billing.Invoice-Paid ",
            1,
            "Invoice paid",
            null,
            NotificationSeverity.Info,
            "{}");

        Assert.Equal("billing.invoice-paid", integrationEvent.NotificationName);
    }

    [Fact]
    public void Event_rejects_invalid_payload_json()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new UserNotificationRequestedIntegrationEvent(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "tenant-a",
                DateTimeOffset.UtcNow,
                "user-a",
                "catalog",
                "catalog-item-updated",
                1,
                "Item updated",
                null,
                NotificationSeverity.Info,
                "{"));

        Assert.Equal("payloadJson", exception.ParamName);
    }

    [Fact]
    public void Event_rejects_payload_json_over_limit()
    {
        string payloadJson = "{\"data\":\"" + new string('a', UserNotificationRequestedIntegrationEvent.PayloadJsonMaxLength) + "\"}";

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new UserNotificationRequestedIntegrationEvent(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "tenant-a",
                DateTimeOffset.UtcNow,
                "user-a",
                "catalog",
                "catalog-item-updated",
                1,
                "Item updated",
                null,
                NotificationSeverity.Info,
                payloadJson));

        Assert.Equal("payloadJson", exception.ParamName);
    }

    [Theory]
    [InlineData(NotificationSeverity.Unknown)]
    [InlineData((NotificationSeverity)999)]
    public void Event_rejects_unknown_or_undefined_severity(NotificationSeverity severity)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserNotificationRequestedIntegrationEvent(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "tenant-a",
                DateTimeOffset.UtcNow,
                "user-a",
                "catalog",
                "catalog-item-updated",
                1,
                "Item updated",
                null,
                severity,
                "{}"));

        Assert.Equal("severity", exception.ParamName);
    }

    [Fact]
    public void Subject_helper_uses_producer_module_subject_shape()
    {
        Assert.Equal(
            IntegrationEventNaming.CreateSubject(
                "catalog",
                UserNotificationRequestedIntegrationEvent.EventType,
                UserNotificationRequestedIntegrationEvent.EventVersion),
            NotificationsIntegrationSubjects.CreateUserNotificationRequested("catalog"));
    }

    [Fact]
    public void Explicit_subscription_helper_registers_producer_specific_handler()
    {
        ServiceCollection services = new();

        services
            .AddNotificationsApplication()
            .AddUserNotificationRequestSubscription("catalog");

        using ServiceProvider provider = services.BuildServiceProvider();
        IntegrationEventSubscription subscription = Assert.Single(
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>().Subscriptions);

        Assert.Equal(NotificationsModuleMetadata.Name, subscription.ConsumerModule);
        Assert.Equal("catalog", subscription.ProducerModule);
        Assert.Equal(UserNotificationRequestedIntegrationEvent.EventType, subscription.EventName);
        Assert.Equal(UserNotificationRequestedIntegrationEvent.EventVersion, subscription.Version);
        Assert.Equal("catalog-notification-request", subscription.HandlerName);
        Assert.Equal(
            NotificationsIntegrationSubjects.CreateUserNotificationRequested("catalog"),
            subscription.Subject);
    }
}
