namespace Gma.Modules.Auth.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class MemberStatusJsonConverter : JsonConverter<MemberStatus>
{
    public override MemberStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Member status must be a string.");
        }

        return MemberStatusNames.TryParse(reader.GetString(), out MemberStatus status)
            ? status
            : throw new JsonException("Member status is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        MemberStatus value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(MemberStatusNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Member status is invalid.", exception);
        }
    }
}
