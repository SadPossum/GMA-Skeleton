namespace Ordering.Tests;

using System.Text.Json;
using Ordering.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrderContractEnumJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Order_status_json_uses_stable_string_names()
    {
        OrderDto order = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "user-1",
            Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            "SKU-1",
            "Catalog item",
            10m,
            "USD",
            "US",
            2,
            20m,
            OrderStatus.Submitted,
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero));

        string json = JsonSerializer.Serialize(order, JsonOptions);

        Assert.Contains("\"status\":\"submitted\"", json, StringComparison.Ordinal);
        Assert.Equal(
            OrderStatus.Submitted,
            JsonSerializer.Deserialize<OrderStatus>("\"Submitted\"", JsonOptions));
    }

    [Fact]
    public void Order_status_names_use_stable_wire_names()
    {
        Assert.Equal("submitted", OrderStatusNames.ToWireName(OrderStatus.Submitted));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unknown\"")]
    [InlineData("\"paid\"")]
    public void Order_status_json_rejects_numeric_or_unknown_values(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderStatus>(json, JsonOptions));
    }

    [Fact]
    public void Order_status_json_rejects_unknown_writes()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(OrderStatus.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((OrderStatus)999, JsonOptions));
    }
}
