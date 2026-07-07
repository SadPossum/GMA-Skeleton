namespace Gma.Modules.Auth.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class UsernameTypeInputJsonConverter : JsonConverter<UsernameTypeInput>
{
    public override UsernameTypeInput Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        return UsernameTypeInput.FromJsonElement(document.RootElement);
    }

    public override void Write(
        Utf8JsonWriter writer,
        UsernameTypeInput value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(UsernameTypeNames.ToWireName(value.Value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Username type is invalid.", exception);
        }
    }
}
