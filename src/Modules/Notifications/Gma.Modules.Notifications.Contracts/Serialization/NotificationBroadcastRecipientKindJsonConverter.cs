namespace Gma.Modules.Notifications.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NotificationBroadcastRecipientKindJsonConverter : JsonConverter<NotificationBroadcastRecipientKind>
{
    public override NotificationBroadcastRecipientKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        NotificationContractEnumJson.ReadString(
            ref reader,
            "Notification broadcast recipient kind",
            NotificationContractEnumJson.ParseRecipientKind);

    public override void Write(
        Utf8JsonWriter writer,
        NotificationBroadcastRecipientKind value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(NotificationContractEnumJson.FormatRecipientKind(value));
}
