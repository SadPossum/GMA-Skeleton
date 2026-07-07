namespace Gma.Modules.Auth.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class UsernameTypeJsonConverter : JsonConverter<UsernameType>
{
    public override UsernameType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Username type must be a string.");
        }

        return UsernameTypeNames.TryParse(reader.GetString(), out UsernameType usernameType)
            ? usernameType
            : throw new JsonException("Username type is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        UsernameType value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(UsernameTypeNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Username type is invalid.", exception);
        }
    }
}
