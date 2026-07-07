namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NotificationBroadcastAudienceJsonConverter : JsonConverter<NotificationBroadcastAudience>
{
    public override NotificationBroadcastAudience Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        NotificationContractEnumJson.ReadString(
            ref reader,
            "Notification broadcast audience",
            NotificationContractEnumJson.ParseAudience);

    public override void Write(
        Utf8JsonWriter writer,
        NotificationBroadcastAudience value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(NotificationContractEnumJson.FormatAudience(value));
}
