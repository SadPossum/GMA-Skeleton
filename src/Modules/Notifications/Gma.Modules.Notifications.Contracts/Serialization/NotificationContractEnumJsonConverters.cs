namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NotificationSeverityJsonConverter : JsonConverter<NotificationSeverity>
{
    public override NotificationSeverity Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        NotificationContractEnumJson.ReadString(
            ref reader,
            "Notification severity",
            NotificationContractEnumJson.ParseSeverity);

    public override void Write(
        Utf8JsonWriter writer,
        NotificationSeverity value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(NotificationContractEnumJson.FormatSeverity(value));
}

internal static class NotificationContractEnumJson
{
    public static TEnum ReadString<TEnum>(
        ref Utf8JsonReader reader,
        string displayName,
        Func<string?, TEnum?> parse)
        where TEnum : struct, Enum
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException($"{displayName} must be a string.");
        }

        TEnum? parsed = parse(reader.GetString());
        return parsed ?? throw new JsonException($"{displayName} is invalid.");
    }

    public static NotificationSeverity? ParseSeverity(string? value)
    {
        string normalized = Normalize(value);
        if (Enum.TryParse(normalized, ignoreCase: true, out NotificationSeverity severity) &&
            severity is not NotificationSeverity.Unknown &&
            Enum.IsDefined(severity))
        {
            return severity;
        }

        return normalized switch
        {
            "info" => NotificationSeverity.Info,
            "success" => NotificationSeverity.Success,
            "warning" => NotificationSeverity.Warning,
            "error" => NotificationSeverity.Error,
            _ => null
        };
    }

    public static string FormatSeverity(NotificationSeverity severity) =>
        severity switch
        {
            NotificationSeverity.Info => "info",
            NotificationSeverity.Success => "success",
            NotificationSeverity.Warning => "warning",
            NotificationSeverity.Error => "error",
            _ => throw new JsonException("Notification severity is invalid.")
        };

    public static NotificationBroadcastAudience? ParseAudience(string? value)
    {
        string normalized = Normalize(value);
        if (Enum.TryParse(normalized, ignoreCase: true, out NotificationBroadcastAudience audience) &&
            audience is not NotificationBroadcastAudience.Unknown &&
            Enum.IsDefined(audience))
        {
            return audience;
        }

        return normalized switch
        {
            "tenant-users" => NotificationBroadcastAudience.TenantUsers,
            "tenant-admins" => NotificationBroadcastAudience.TenantAdmins,
            "platform-users" => NotificationBroadcastAudience.PlatformUsers,
            "platform-admins" => NotificationBroadcastAudience.PlatformAdmins,
            _ => null
        };
    }

    public static string FormatAudience(NotificationBroadcastAudience audience) =>
        audience switch
        {
            NotificationBroadcastAudience.TenantUsers => "tenant-users",
            NotificationBroadcastAudience.TenantAdmins => "tenant-admins",
            NotificationBroadcastAudience.PlatformUsers => "platform-users",
            NotificationBroadcastAudience.PlatformAdmins => "platform-admins",
            _ => throw new JsonException("Notification broadcast audience is invalid.")
        };

    public static NotificationBroadcastRecipientKind? ParseRecipientKind(string? value)
    {
        string normalized = Normalize(value);
        if (Enum.TryParse(normalized, ignoreCase: true, out NotificationBroadcastRecipientKind recipientKind) &&
            recipientKind is not NotificationBroadcastRecipientKind.Unknown &&
            Enum.IsDefined(recipientKind))
        {
            return recipientKind;
        }

        return normalized switch
        {
            "user" => NotificationBroadcastRecipientKind.User,
            "admin" => NotificationBroadcastRecipientKind.Admin,
            _ => null
        };
    }

    public static string FormatRecipientKind(NotificationBroadcastRecipientKind recipientKind) =>
        recipientKind switch
        {
            NotificationBroadcastRecipientKind.User => "user",
            NotificationBroadcastRecipientKind.Admin => "admin",
            _ => throw new JsonException("Notification broadcast recipient kind is invalid.")
        };

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();
}
