namespace Gma.Modules.Auth.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonConverter(typeof(UsernameTypeInputJsonConverter))]
public readonly record struct UsernameTypeInput
{
    public UsernameTypeInput(UsernameType value) => this.Value = value;

    public UsernameType Value { get; }

    public static UsernameTypeInput FromJsonElement(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.String &&
            UsernameTypeNames.TryParse(value.GetString(), out UsernameType usernameType))
        {
            return new UsernameTypeInput(usernameType);
        }

        return new UsernameTypeInput(UsernameType.Unknown);
    }
}
