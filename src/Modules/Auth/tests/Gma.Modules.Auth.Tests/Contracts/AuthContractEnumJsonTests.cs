namespace Gma.Modules.Auth.Tests;

using System.Text.Json;
using Gma.Modules.Auth.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthContractEnumJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Username_type_json_uses_stable_string_names()
    {
        RegisterMemberRequest request = new("user@example.com", UsernameType.Email, "secret-password");

        string json = JsonSerializer.Serialize(request, JsonOptions);

        Assert.Contains("\"usernameType\":\"email\"", json, StringComparison.Ordinal);
        Assert.Equal(
            UsernameType.Phone,
            JsonSerializer.Deserialize<UsernameType>("\"phone\"", JsonOptions));
        Assert.Equal(
            UsernameType.Phone,
            JsonSerializer.Deserialize<UsernameType>("\"Phone\"", JsonOptions));
    }

    [Theory]
    [InlineData(UsernameType.Email, "email")]
    [InlineData(UsernameType.Phone, "phone")]
    public void Username_type_names_use_stable_wire_names(UsernameType usernameType, string expected)
    {
        Assert.Equal(expected, UsernameTypeNames.ToWireName(usernameType));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unknown\"")]
    [InlineData("\"social\"")]
    public void Username_type_json_rejects_numeric_or_unknown_values(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UsernameType>(json, JsonOptions));
    }

    [Fact]
    public void Username_type_json_rejects_unknown_writes()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(UsernameType.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((UsernameType)999, JsonOptions));
    }

    [Theory]
    [InlineData("\"email\"", UsernameType.Email)]
    [InlineData("\"phone\"", UsernameType.Phone)]
    [InlineData("\"unknown\"", UsernameType.Unknown)]
    [InlineData("\"telegram\"", UsernameType.Unknown)]
    [InlineData("999", UsernameType.Unknown)]
    public void Username_type_input_maps_invalid_values_to_unknown(string json, UsernameType expected)
    {
        UsernameTypeInput input = JsonSerializer.Deserialize<UsernameTypeInput>(json, JsonOptions);

        Assert.Equal(expected, input.Value);
    }

    [Fact]
    public void Member_status_json_uses_stable_string_names()
    {
        AdminMemberDetails details = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "tenant-a",
            MemberStatus.Disabled,
            "user@example.com",
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 5, 13, 0, 0, TimeSpan.Zero),
            "support action",
            0,
            3);

        string json = JsonSerializer.Serialize(details, JsonOptions);

        Assert.Contains("\"status\":\"disabled\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MemberStatus.Active, "active")]
    [InlineData(MemberStatus.Disabled, "disabled")]
    public void Member_status_names_use_stable_wire_names(MemberStatus status, string expected)
    {
        Assert.Equal(expected, MemberStatusNames.ToWireName(status));
    }

    [Theory]
    [InlineData("\"active\"", MemberStatus.Active)]
    [InlineData("\"Active\"", MemberStatus.Active)]
    [InlineData("\"disabled\"", MemberStatus.Disabled)]
    public void Member_status_json_reads_valid_names(string json, MemberStatus expected)
    {
        MemberStatus status = JsonSerializer.Deserialize<MemberStatus>(json, JsonOptions);

        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unknown\"")]
    [InlineData("\"future\"")]
    public void Member_status_json_rejects_numeric_or_unknown_values(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemberStatus>(json, JsonOptions));
    }

    [Fact]
    public void Member_status_json_rejects_unknown_writes()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(MemberStatus.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((MemberStatus)999, JsonOptions));
    }
}
