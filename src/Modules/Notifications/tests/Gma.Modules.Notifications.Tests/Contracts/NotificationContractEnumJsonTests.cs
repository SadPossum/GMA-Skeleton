namespace Gma.Modules.Notifications.Tests;

using System.Text.Json;
using Gma.Modules.Notifications.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class NotificationContractEnumJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Contract_enums_serialize_stable_wire_names()
    {
        Assert.Equal("\"success\"", JsonSerializer.Serialize(NotificationSeverity.Success, JsonOptions));
        Assert.Equal(
            "\"tenant-admins\"",
            JsonSerializer.Serialize(NotificationBroadcastAudience.TenantAdmins, JsonOptions));
        Assert.Equal(
            "\"admin\"",
            JsonSerializer.Serialize(NotificationBroadcastRecipientKind.Admin, JsonOptions));
    }

    [Fact]
    public void Contract_enums_deserialize_stable_wire_names_and_enum_names()
    {
        Assert.Equal(
            NotificationSeverity.Warning,
            JsonSerializer.Deserialize<NotificationSeverity>("\"warning\"", JsonOptions));
        Assert.Equal(
            NotificationSeverity.Warning,
            JsonSerializer.Deserialize<NotificationSeverity>("\"Warning\"", JsonOptions));
        Assert.Equal(
            NotificationBroadcastAudience.TenantAdmins,
            JsonSerializer.Deserialize<NotificationBroadcastAudience>("\"tenant-admins\"", JsonOptions));
        Assert.Equal(
            NotificationBroadcastAudience.TenantAdmins,
            JsonSerializer.Deserialize<NotificationBroadcastAudience>("\"TenantAdmins\"", JsonOptions));
        Assert.Equal(
            NotificationBroadcastRecipientKind.Admin,
            JsonSerializer.Deserialize<NotificationBroadcastRecipientKind>("\"admin\"", JsonOptions));
        Assert.Equal(
            NotificationBroadcastRecipientKind.Admin,
            JsonSerializer.Deserialize<NotificationBroadcastRecipientKind>("\"Admin\"", JsonOptions));
    }

    [Fact]
    public void Contract_enums_reject_numeric_unknown_or_undefined_values()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSeverity>("3", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationSeverity>("\"unknown\"", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(NotificationSeverity.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((NotificationSeverity)999, JsonOptions));

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationBroadcastAudience>("2", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationBroadcastAudience>("\"unknown\"", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(NotificationBroadcastAudience.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((NotificationBroadcastAudience)999, JsonOptions));

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationBroadcastRecipientKind>("2", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationBroadcastRecipientKind>("\"unknown\"", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(NotificationBroadcastRecipientKind.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((NotificationBroadcastRecipientKind)999, JsonOptions));
    }

    [Fact]
    public void Durable_notification_request_event_round_trips_with_text_severity()
    {
        UserNotificationRequestedIntegrationEvent integrationEvent = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero),
            "user-a",
            "catalog",
            "catalog.item-updated",
            1,
            "Item updated",
            "Catalog item changed.",
            NotificationSeverity.Success,
            "{\"sku\":\"SKU-1\"}");

        string json = JsonSerializer.Serialize(integrationEvent, JsonOptions);
        UserNotificationRequestedIntegrationEvent? deserialized =
            JsonSerializer.Deserialize<UserNotificationRequestedIntegrationEvent>(json, JsonOptions);

        Assert.Contains("\"severity\":\"success\"", json, StringComparison.Ordinal);
        Assert.NotNull(deserialized);
        Assert.Equal(NotificationSeverity.Success, deserialized.Severity);
        Assert.Equal(integrationEvent.PayloadJson, deserialized.PayloadJson);
    }
}
