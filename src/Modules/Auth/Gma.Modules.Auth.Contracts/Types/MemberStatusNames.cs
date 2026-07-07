namespace Gma.Modules.Auth.Contracts;

public static class MemberStatusNames
{
    public static string ToWireName(MemberStatus status) =>
        status switch
        {
            MemberStatus.Active => "active",
            MemberStatus.Disabled => "disabled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Member status is invalid.")
        };

    public static bool TryParse(string? value, out MemberStatus status)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Enum.TryParse(normalized, ignoreCase: true, out status) &&
            status is not MemberStatus.Unknown &&
            Enum.IsDefined(status))
        {
            return true;
        }

        status = normalized switch
        {
            "active" => MemberStatus.Active,
            "disabled" => MemberStatus.Disabled,
            _ => MemberStatus.Unknown
        };

        return status is not MemberStatus.Unknown;
    }
}
