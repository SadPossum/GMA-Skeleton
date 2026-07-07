namespace Gma.Modules.Auth.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(UsernameTypeJsonConverter))]
public enum UsernameType
{
    Unknown = 0,
    Email = 1,
    Phone = 2
}
