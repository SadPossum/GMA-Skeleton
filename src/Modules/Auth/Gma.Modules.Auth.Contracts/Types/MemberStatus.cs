namespace Gma.Modules.Auth.Contracts;

using System.Text.Json.Serialization;

[JsonConverter(typeof(MemberStatusJsonConverter))]
public enum MemberStatus
{
    Unknown = 0,
    Active = 1,
    Disabled = 2
}
